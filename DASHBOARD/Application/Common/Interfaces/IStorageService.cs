namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>
/// Abstraction over an S3-compatible object store (MinIO in development, AWS S3 in production).
/// Handlers depend only on this interface — the concrete implementation lives in Infrastructure.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Generates a presigned PUT URL that allows the browser to upload a file directly to the
    /// object store without routing the binary through the API server.
    /// </summary>
    /// <param name="bucket">Target bucket name.</param>
    /// <param name="objectKey">Object key (path) within the bucket, e.g. "avatars/user-id/1234.jpg".</param>
    /// <param name="expiry">How long the URL remains valid (max 7 days for AWS S3; MinIO supports longer).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A presigned URL the client can PUT the file to directly.</returns>
    Task<string> GenerateUploadUrlAsync(string bucket, string objectKey, TimeSpan expiry, CancellationToken ct);

    /// <summary>
    /// Returns the public URL for an already-stored object so it can be embedded in API responses
    /// or stored as a persistent reference (e.g. in <c>UserSetting</c>).
    /// </summary>
    /// <param name="bucket">Bucket the object lives in.</param>
    /// <param name="objectKey">Object key within the bucket.</param>
    /// <returns>The fully-qualified public URL of the object.</returns>
    string GetPublicUrl(string bucket, string objectKey);

    /// <summary>
    /// Deletes an object from the store. Used when replacing an avatar — removes the old file
    /// so stale objects do not accumulate.
    /// No-op (does not throw) if the object does not exist.
    /// </summary>
    /// <param name="bucket">Bucket the object lives in.</param>
    /// <param name="objectKey">Object key to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(string bucket, string objectKey, CancellationToken ct);

    /// <summary>
    /// Ensures the bucket exists, creating it with public-read policy if it does not.
    /// Call once at application startup for each bucket the app uses.
    /// </summary>
    /// <param name="bucket">Bucket to ensure exists.</param>
    /// <param name="ct">Cancellation token.</param>
    Task EnsureBucketExistsAsync(string bucket, CancellationToken ct);

    /// <summary>
    /// Fetches only the metadata of an object (no data transfer) to confirm it exists.
    /// Throws <see cref="Amazon.S3.AmazonS3Exception"/> with 404 status if not found.
    /// </summary>
    /// <param name="bucket">Bucket the object lives in.</param>
    /// <param name="objectKey">Object key to check.</param>
    /// <param name="ct">Cancellation token.</param>
    Task HeadObjectAsync(string bucket, string objectKey, CancellationToken ct);

    // ── Multipart upload (for files ≥ 5 MB) ──────────────────────────────────

    /// <summary>
    /// Initiates a multipart upload session and returns the upload ID assigned by the store.
    /// Use for files ≥ 5 MB; for smaller files prefer <see cref="GenerateUploadUrlAsync"/>.
    /// </summary>
    /// <param name="bucket">Target bucket.</param>
    /// <param name="objectKey">Object key within the bucket.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The upload ID that must be passed to subsequent multipart calls.</returns>
    Task<string> InitiateMultipartUploadAsync(string bucket, string objectKey, CancellationToken ct);

    /// <summary>
    /// Generates presigned PUT URLs for each chunk of a multipart upload.
    /// Each part except the last must be ≥ 5 MB (S3/MinIO constraint).
    /// </summary>
    /// <param name="bucket">Target bucket.</param>
    /// <param name="objectKey">Object key within the bucket.</param>
    /// <param name="uploadId">Upload ID from <see cref="InitiateMultipartUploadAsync"/>.</param>
    /// <param name="partCount">Total number of parts (chunks) to upload.</param>
    /// <param name="expiry">How long each presigned URL remains valid.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of presigned PUT URLs, one per part, ordered by part number (1-based).</returns>
    Task<IReadOnlyList<string>> GeneratePartUploadUrlsAsync(
        string bucket,
        string objectKey,
        string uploadId,
        int partCount,
        TimeSpan expiry,
        CancellationToken ct);

    /// <summary>
    /// Completes a multipart upload by instructing the store to assemble all uploaded parts
    /// into the final object. Must be called after all parts have been PUT successfully.
    /// </summary>
    /// <param name="bucket">Target bucket.</param>
    /// <param name="objectKey">Object key within the bucket.</param>
    /// <param name="uploadId">Upload ID from <see cref="InitiateMultipartUploadAsync"/>.</param>
    /// <param name="parts">Ordered list of (partNumber, eTag) pairs returned by MinIO for each PUT.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CompleteMultipartUploadAsync(
        string bucket,
        string objectKey,
        string uploadId,
        IReadOnlyList<(int PartNumber, string ETag)> parts,
        CancellationToken ct);

    /// <summary>
    /// Aborts an in-progress multipart upload and releases any partially uploaded data.
    /// Call when the client cancels mid-upload to avoid orphaned parts accumulating in the store.
    /// </summary>
    /// <param name="bucket">Target bucket.</param>
    /// <param name="objectKey">Object key of the aborted upload.</param>
    /// <param name="uploadId">Upload ID from <see cref="InitiateMultipartUploadAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AbortMultipartUploadAsync(string bucket, string objectKey, string uploadId, CancellationToken ct);
}
