namespace CoffeeNChill.Gateway.Services;

/// <summary>Staff document action that talks to the staff-docs container directly.</summary>
public interface IDocumentStorageService
{
    Task DeleteAsync(string fileName, CancellationToken ct);
}
