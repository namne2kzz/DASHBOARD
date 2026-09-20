namespace DASHBOARD.Application.Common.Exceptions;

/// <summary>
/// Thrown by <see cref="Interfaces.IStorageService.HeadObjectAsync"/> when the requested object
/// does not exist in the store. Wraps the provider-specific 404 so Application layer handlers
/// do not need to reference AWS SDK or MinIO SDK types directly.
/// </summary>
public sealed class StorageObjectNotFoundException : Exception
{
    /// <summary>Initialises the exception for a missing storage object.</summary>
    /// <param name="bucket">Bucket that was queried.</param>
    /// <param name="objectKey">Key that was not found.</param>
    public StorageObjectNotFoundException(string bucket, string objectKey)
        : base($"Object '{objectKey}' not found in bucket '{bucket}'.") { }
}
