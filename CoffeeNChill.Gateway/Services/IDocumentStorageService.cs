namespace CoffeeNChill.Gateway.Services;

/// Staff document action that talks to the staff-docs container directly.
public interface IDocumentStorageService
{
    Task DeleteAsync(string fileName, CancellationToken ct);
}
