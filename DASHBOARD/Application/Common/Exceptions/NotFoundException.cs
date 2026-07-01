namespace DASHBOARD.Application.Common.Exceptions;

/// <summary>Thrown when a requested entity cannot be found in the data store.</summary>
public sealed class NotFoundException : Exception
{
    /// <summary>Initializes a new <see cref="NotFoundException"/> for a specific entity type and key.</summary>
    /// <param name="entityName">The name of the entity type that was not found.</param>
    /// <param name="key">The key or identifier that was looked up.</param>
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.") { }
}
