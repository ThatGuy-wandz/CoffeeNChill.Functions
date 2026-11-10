using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoffeeNChill.Gateway.Exceptions;

namespace CoffeeNChill.Gateway.Services;


/// Forwards requests to the Azure Functions app and turns its answers into contracts.
/// The base address comes from configuration (FunctionsApi:BaseUrl), set in Program.cs.

public class FunctionsApiClient : IFunctionsApiClient
{
    // The Functions app writes camelCase JSON; "Web" defaults read and write that.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<FunctionsApiClient> _logger;

    public FunctionsApiClient(HttpClient http, ILogger<FunctionsApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // ---------- Menu ----------

    public Task<List<FnMenuItem>> GetMenuAsync(CancellationToken ct) =>
        GetAsync<List<FnMenuItem>>("menu", ct);

    public Task<List<FnMenuItem>> GetMenuByCategoryAsync(string category, CancellationToken ct) =>
        GetAsync<List<FnMenuItem>>($"menu/category/{Escape(category)}", ct);

    public Task<FnMenuItem> CreateMenuItemAsync(FnMenuItemCreate payload, CancellationToken ct) =>
        SendJsonAsync<FnMenuItem>(HttpMethod.Post, "menu", payload, ct);

    public Task<FnMenuItem> UpdateMenuItemAsync(
        string category, string id, FnMenuItemUpdate payload, CancellationToken ct) =>
        SendJsonAsync<FnMenuItem>(HttpMethod.Put, $"menu/{Escape(category)}/{Escape(id)}", payload, ct);

    public async Task DeleteMenuItemAsync(string category, string id, CancellationToken ct)
    {
        using var response = await _http.DeleteAsync($"menu/{Escape(category)}/{Escape(id)}", ct);
        await EnsureSuccessAsync(response, ct);
    }

    // ---------- Orders ----------

    public Task<FnOrderQueued> PlaceOrderAsync(FnOrderRequest payload, CancellationToken ct) =>
        SendJsonAsync<FnOrderQueued>(HttpMethod.Post, "orders/queue", payload, ct);

    public Task<FnOrderStatus> GetOrderStatusAsync(string orderId, CancellationToken ct) =>
        GetAsync<FnOrderStatus>($"orders/status/{Escape(orderId)}", ct);

    public Task<FnOrderList> GetOrdersAsync(string? date, string? status, CancellationToken ct)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(date)) query.Add($"date={Escape(date)}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Escape(status)}");

        var url = query.Count == 0 ? "orders/status" : $"orders/status?{string.Join("&", query)}";
        return GetAsync<FnOrderList>(url, ct);
    }

    // ---------- Staff documents ----------

    public Task<List<FnStaffDocument>> ListDocumentsAsync(CancellationToken ct) =>
        GetAsync<List<FnStaffDocument>>("documents", ct);

    public async Task<FnUploadResult> UploadDocumentAsync(
        Stream content, string fileName, string contentType, CancellationToken ct)
    {
        // Rebuild the multipart/form-data request the Function expects (a part called "file")
        using var form = new MultipartFormDataContent();
        var filePart = new StreamContent(content);
        filePart.Headers.ContentType = MediaTypeHeaderValue.TryParse(contentType, out var parsed)
            ? parsed
            : new MediaTypeHeaderValue("application/octet-stream");
        form.Add(filePart, "file", fileName);

        using var response = await _http.PostAsync("documents/upload", form, ct);
        return await ReadAsync<FnUploadResult>(response, ct);
    }

    public async Task<FnDownload> DownloadDocumentAsync(string fileName, CancellationToken ct)
    {
        // ResponseHeadersRead = start streaming without loading the whole file into memory
        var response = await _http.GetAsync(
            $"documents/download/{Escape(fileName)}", HttpCompletionOption.ResponseHeadersRead, ct);

        try
        {
            await EnsureSuccessAsync(response, ct);
        }
        catch
        {
            response.Dispose();
            throw;
        }

        var stream = await response.Content.ReadAsStreamAsync(ct);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        return new FnDownload(stream, contentType, fileName);
    }

    // ---------- Helpers ----------

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task<T> SendJsonAsync<T>(HttpMethod method, string url, object payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(payload, payload.GetType(), options: JsonOptions)
        };
        using var response = await _http.SendAsync(request, ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return result ?? throw new GatewayException(
            HttpStatusCode.BadGateway, "The Functions app returned an empty response.");
    }

    // 4xx from a Function (bad input, not found, conflict, wrong file type) is passed straight
    // through to the client. 5xx means the Functions app itself failed, which is a 502 for us.
    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var message = await ReadErrorMessageAsync(response, ct);
        var status = (int)response.StatusCode;

        if (status >= 400 && status < 500)
        {
            throw new GatewayException(response.StatusCode, message);
        }

        _logger.LogError("Functions app returned {Status}: {Message}", status, message);
        throw new GatewayException(
            status == 503 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.BadGateway,
            $"The Functions app reported a problem: {message}");
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<FnError>(JsonOptions, ct);
            if (!string.IsNullOrWhiteSpace(error?.Error)) return error.Error;
        }
        catch (JsonException) { }
        catch (NotSupportedException) { }

        return $"The Functions app returned {(int)response.StatusCode} {response.ReasonPhrase}.";
    }
}
