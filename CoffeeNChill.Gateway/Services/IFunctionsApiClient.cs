namespace CoffeeNChill.Gateway.Services;

/// <summary>
/// Everything the gateway asks of the Azure Functions app. Controllers depend on this
/// interface, not on HttpClient, so they stay thin and the routing detail lives in one class.
/// </summary>
public interface IFunctionsApiClient
{
    // Menu (Azure Table)
    Task<List<FnMenuItem>> GetMenuAsync(CancellationToken ct);
    Task<List<FnMenuItem>> GetMenuByCategoryAsync(string category, CancellationToken ct);
    Task<FnMenuItem> CreateMenuItemAsync(FnMenuItemCreate payload, CancellationToken ct);
    Task<FnMenuItem> UpdateMenuItemAsync(string category, string id, FnMenuItemUpdate payload, CancellationToken ct);
    Task DeleteMenuItemAsync(string category, string id, CancellationToken ct);

    // Orders (Azure Queue + Orders table)
    Task<FnOrderQueued> PlaceOrderAsync(FnOrderRequest payload, CancellationToken ct);
    Task<FnOrderStatus> GetOrderStatusAsync(string orderId, CancellationToken ct);
    Task<FnOrderList> GetOrdersAsync(string? date, string? status, CancellationToken ct);

    // Staff documents (Azure File share)
    Task<List<FnStaffDocument>> ListDocumentsAsync(CancellationToken ct);
    Task<FnUploadResult> UploadDocumentAsync(Stream content, string fileName, string contentType, CancellationToken ct);
    Task<FnDownload> DownloadDocumentAsync(string fileName, CancellationToken ct);
}
