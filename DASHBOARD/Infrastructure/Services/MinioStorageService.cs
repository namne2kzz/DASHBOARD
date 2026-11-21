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
    private readonly AmazonS3Client _presignClient;

    /// <summary>Initialises the S3 client pointed at the configured MinIO endpoint.</summary>
    public MinioStorageService(IAppSettings settings, ILogger<MinioStorageService> logger)
    {
        _settings = settings;
        _logger   = logger;

        // Presigned URLs came out Signature V2 (AWSAccessKeyId=…&Signature=…) and MinIO rejected
        // every PUT with SignatureDoesNotMatch. Per the SDK's own docs, the default uses V4 for
        // all requests EXCEPT presigned URLs when the region is us-east-1 — which is the region
        // set below. Setting this explicitly is what forces V4 for presigns too; the config's
        // SignatureVersion property does not govern them.
        AWSConfigsS3.UseSignatureVersion4 = true;

        _client       = BuildClient(settings, settings.MinioEndpoint);

        // Separate client signing against the PUBLIC host, used only to presign URLs. SigV4 signs
        // the Host header (X-Amz-SignedHeaders=host), so rewriting minio:9000 -> localhost:9100
        // after signing invalidated the signature and MinIO answered every browser PUT with 403
        // SignatureDoesNotMatch — verified by replaying one URL: 200 at the signed host, 403 at
        // the rewritten one. Signing for the host the browser actually calls removes the rewrite.
        _presignClient = settings.MinioPublicBaseUrl.TrimEnd('/') == settings.MinioEndpoint.TrimEnd('/')
            ? _client
            : BuildClient(settings, settings.MinioPublicBaseUrl);
    }

    /// <summary>Scheme presigned URLs must use, taken from the public base URL.</summary>
    /// <returns><see cref="Protocol.HTTP"/> for an http:// base, otherwise HTTPS.</returns>
    private Protocol PresignProtocol() =>
        _settings.MinioPublicBaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? Protocol.HTTP
            : Protocol.HTTPS;

    /// <summary>Builds an S3 client for MinIO against the given endpoint.</summary>
    /// <param name="settings">Credentials source.</param>
    /// <param name="endpoint">Base URL this client signs and talks to.</param>
    /// <returns>A configured <see cref="AmazonS3Client"/>.</returns>
    private static AmazonS3Client BuildClient(IAppSettings settings, string endpoint)
    {
        return new AmazonS3Client(
            settings.MinioAccessKey,
            settings.MinioSecretKey,
            new AmazonS3Config
            {
                ServiceURL       = endpoint,
                ForcePathStyle   = true,   // required for MinIO — virtual-hosted style not supported
                SignatureVersion = "4",
                AuthenticationRegion = "us-east-1", // MinIO ignores region but SDK requires a value
                // Without this the SDK signs presigned URLs as https even for an http endpoint,
                // so the browser's PUT fails with ERR_SSL_PROTOCOL_ERROR against the plain-HTTP MinIO.
                UseHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
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
            // AmazonS3Config.UseHttp does not reach presigned URLs — the SDK emitted
            // https://localhost:9100 against a plain-HTTP MinIO and the browser failed with
            // ERR_SSL_PROTOCOL_ERROR. Protocol is the switch that actually applies here.
            Protocol   = PresignProtocol(),
        };

        // GetPreSignedURL is synchronous in AWSSDK.S3 — wrap to satisfy async interface.
        // Signed by the public-host client, so the URL needs no rewriting afterwards.
        return Task.FromResult(_presignClient.GetPreSignedURL(request));
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
                Protocol   = PresignProtocol(),
            };

            var url = _presignClient.GetPreSignedURL(request);
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

}
