using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace DASHBOARD.Infrastructure.Services;

/// <summary>
/// S3-compatible object storage implementation backed by MinIO (or AWS S3 in production).
/// Registered as <see cref="IStorageService"/> in the DI container.
/// </summary>
public sealed class MinioStorageService : IStorageService, IAsyncDisposable
{
    private readonly AmazonS3Client    _client;
    private readonly IAppSettings      _settings;
    private readonly ILogger<MinioStorageService> _logger;

    /// <summary>Initialises the S3 client pointed at the configured MinIO endpoint.</summary>
    public MinioStorageService(IAppSettings settings, ILogger<MinioStorageService> logger)
    {
        _settings = settings;
        _logger   = logger;

        _client = new AmazonS3Client(
            settings.MinioAccessKey,
            settings.MinioSecretKey,
            new AmazonS3Config
            {
                ServiceURL       = settings.MinioEndpoint,
                ForcePathStyle   = true,   // required for MinIO — virtual-hosted style not supported
                SignatureVersion = "4",
                AuthenticationRegion = "us-east-1", // MinIO ignores region but SDK requires a value
            });
    }

    // ── Simple upload ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<string> GenerateUploadUrlAsync(
        string bucket, string objectKey, TimeSpan expiry, CancellationToken ct)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key        = objectKey,
            Verb       = HttpVerb.PUT,
            Expires    = DateTime.UtcNow.Add(expiry),
        };

        // GetPreSignedURL is synchronous in AWSSDK.S3 — wrap to satisfy async interface.
        var url = _client.GetPreSignedURL(request);

        // Swap the internal Docker endpoint for the public base URL so the browser can reach it.
        url = ReplaceEndpoint(url);

        return Task.FromResult(url);
    }

    /// <inheritdoc/>
    public string GetPublicUrl(string bucket, string objectKey) =>
        $"{_settings.MinioPublicBaseUrl.TrimEnd('/')}/{bucket}/{objectKey}";

    /// <inheritdoc/>
    public async Task DeleteAsync(string bucket, string objectKey, CancellationToken ct)
    {
        try
        {
            await _client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = bucket,
                Key        = objectKey,
            }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // No-op — object does not exist, nothing to delete.
            _logger.LogDebug("DeleteAsync: object {Key} not found in {Bucket}, skipping", objectKey, bucket);
        }
    }

    /// <inheritdoc/>
    public async Task EnsureBucketExistsAsync(string bucket, CancellationToken ct)
    {
        try
        {
            await _client.PutBucketAsync(new PutBucketRequest
            {
                BucketName       = bucket,
                UseClientRegion  = true,
            }, ct);

            // Set bucket policy to allow anonymous GET (public-read).
            var policy = $$"""
                {
                  "Version": "2012-10-17",
                  "Statement": [{
                    "Effect":    "Allow",
                    "Principal": "*",
                    "Action":    "s3:GetObject",
                    "Resource":  "arn:aws:s3:::{{bucket}}/*"
                  }]
                }
                """;

            await _client.PutBucketPolicyAsync(new PutBucketPolicyRequest
            {
                BucketName = bucket,
                Policy     = policy,
            }, ct);

            _logger.LogInformation("Storage: created bucket '{Bucket}' with public-read policy", bucket);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
            // Bucket already exists — nothing to do.
            _logger.LogDebug("Storage: bucket '{Bucket}' already exists", bucket);
        }
    }

    /// <inheritdoc/>
    public async Task HeadObjectAsync(string bucket, string objectKey, CancellationToken ct)
    {
        try
        {
            await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = bucket,
                Key        = objectKey,
            }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new StorageObjectNotFoundException(bucket, objectKey);
        }
    }

    // ── Multipart upload ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<string> InitiateMultipartUploadAsync(
        string bucket, string objectKey, CancellationToken ct)
    {
        var response = await _client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
        {
            BucketName = bucket,
            Key        = objectKey,
        }, ct);

        return response.UploadId;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GeneratePartUploadUrlsAsync(
        string bucket,
        string objectKey,
        string uploadId,
        int partCount,
        TimeSpan expiry,
        CancellationToken ct)
    {
        var urls = new List<string>(partCount);
        var expires = DateTime.UtcNow.Add(expiry);

        for (var i = 1; i <= partCount; i++)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucket,
                Key        = objectKey,
                Verb       = HttpVerb.PUT,
                Expires    = expires,
                UploadId   = uploadId,
                PartNumber = i,
            };

            var url = ReplaceEndpoint(_client.GetPreSignedURL(request));
            urls.Add(url);
        }

        return Task.FromResult<IReadOnlyList<string>>(urls);
    }

    /// <inheritdoc/>
    public async Task CompleteMultipartUploadAsync(
        string bucket,
        string objectKey,
        string uploadId,
        IReadOnlyList<(int PartNumber, string ETag)> parts,
        CancellationToken ct)
    {
        await _client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
        {
            BucketName = bucket,
            Key        = objectKey,
            UploadId   = uploadId,
            PartETags  = parts.Select(p => new PartETag(p.PartNumber, p.ETag)).ToList(),
        }, ct);
    }

    /// <inheritdoc/>
    public async Task AbortMultipartUploadAsync(
        string bucket, string objectKey, string uploadId, CancellationToken ct)
    {
        try
        {
            await _client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
            {
                BucketName = bucket,
                Key        = objectKey,
                UploadId   = uploadId,
            }, ct);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex,
                "AbortMultipartUpload failed for uploadId {UploadId} on {Key} — orphaned parts may remain",
                uploadId, objectKey);
        }
    }

    // ── IAsyncDisposable ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the internal MinIO endpoint host in a presigned URL with the public base URL.
    /// Needed in Docker: the SDK signs using <c>MinioEndpoint</c> (e.g. http://minio:9000) but the
    /// browser must reach <c>MinioPublicBaseUrl</c> (e.g. http://localhost:9100).
    /// </summary>
    private string ReplaceEndpoint(string presignedUrl)
    {
        var internalBase = _settings.MinioEndpoint.TrimEnd('/');
        var publicBase   = _settings.MinioPublicBaseUrl.TrimEnd('/');

        return internalBase == publicBase
            ? presignedUrl
            : presignedUrl.Replace(internalBase, publicBase, StringComparison.OrdinalIgnoreCase);
    }
}
