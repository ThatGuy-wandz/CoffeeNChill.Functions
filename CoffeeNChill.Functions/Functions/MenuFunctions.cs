using System.Net;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
namespace CoffeeNChill.Functions.Functions
{
    public class MenuFunctions
    {
        private readonly TableClient _tableClient;
        private readonly ILogger<MenuFunctions> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        public MenuFunctions(TableClient tableClient, ILogger<MenuFunctions> logger)
        {
            _tableClient = tableClient;
            _logger = logger;
        }
  
        // POST /api/menu -> create a new menu item
      
        [Function("CreateMenuItem")]
        public async Task<HttpResponseData> CreateMenuItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "menu")] HttpRequestData req)
        {
            _logger.LogInformation("Creating a new menu item.");
            MenuItemCreateDto? dto;
            try
            {
                var body = await req.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(body))
                    return await Error(req, HttpStatusCode.BadRequest, "Request body cannot be empty.");
                dto = JsonSerializer.Deserialize<MenuItemCreateDto>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return await Error(req, HttpStatusCode.BadRequest, "Malformed JSON in request body.");
            }
            if (dto is null)
                return await Error(req, HttpStatusCode.BadRequest, "Request body could not be parsed.");
            var validationError = ValidateCreateDto(dto);
            if (validationError is not null)
                return await Error(req, HttpStatusCode.BadRequest, validationError);
            var entity = new MenuItem
            {
                PartitionKey = dto.Category.Trim(),
                RowKey = dto.Sku.Trim(),
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim() ?? string.Empty,
                Price = dto.Price,
                IsAvailable = dto.IsAvailable
            };
            try
            {
                await _tableClient.AddEntityAsync(entity);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                return await Error(req, HttpStatusCode.Conflict,
                $"A menu item with SKU '{entity.RowKey}' already exists in category '{entity.PartitionKey}'.");
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Table Storage error while creating menu item.");
                return await Error(req, HttpStatusCode.InternalServerError, "Failed to save the menu item.");
            }
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(MenuItemResponseDto.FromEntity(entity));
            return response;
        }
       
        // GET /api/menu -> return every menu item
       
        [Function("GetAllMenuItems")]
        public async Task<HttpResponseData> GetAllMenuItems(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu")] HttpRequestData req)
        {
            _logger.LogInformation("Retrieving all menu items.");
            var items = new List<MenuItemResponseDto>();
            try
            {
                await foreach (var entity in _tableClient.QueryAsync<MenuItem>())
                {
                    items.Add(MenuItemResponseDto.FromEntity(entity));
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Table Storage error while retrieving menu items.");
                return await Error(req, HttpStatusCode.InternalServerError, "Failed to retrieve menu items.");
            }
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(items);
            return response;
        }
        
        // GET /api/menu/category/{category} -> filter by PartitionKey:
            
        [Function("GetMenuItemsByCategory")]
        public async Task<HttpResponseData> GetMenuItemsByCategory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/category/{category}")] HttpRequestData req,
        string category)
        {
            _logger.LogInformation("Retrieving menu items for category {Category}.", category);
            if (string.IsNullOrWhiteSpace(category))
                return await Error(req, HttpStatusCode.BadRequest, "Category route parameter is required.");
            var items = new List<MenuItemResponseDto>();
            try
            {
                await foreach (var entity in _tableClient.QueryAsync<MenuItem>(e => e.PartitionKey == category))
                {
                    items.Add(MenuItemResponseDto.FromEntity(entity));
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Table Storage error while filtering by category.");
                return await Error(req, HttpStatusCode.InternalServerError, "Failed to retrieve menu items for category.");
            }
            if (items.Count == 0)
                return await Error(req, HttpStatusCode.NotFound, $"No menu items found in category '{category}'.");
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(items);
            return response;
        }
       
        // PUT /api/menu/{category}/{id} -> update price and/or availability:
        
        [Function("UpdateMenuItem")]
        public async Task<HttpResponseData> UpdateMenuItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "menu/{category}/{id}")] HttpRequestData req,
        string category, string id)
        {
            _logger.LogInformation("Updating menu item {Category}/{Id}.", category, id);
            MenuItemUpdateDto? dto;
            try
            {
                var body = await req.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(body))
                    return await Error(req, HttpStatusCode.BadRequest, "Request body cannot be empty.");
                dto = JsonSerializer.Deserialize<MenuItemUpdateDto>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return await Error(req, HttpStatusCode.BadRequest, "Malformed JSON in request body.");
            }
            if (dto is null || (dto.Price is null && dto.IsAvailable is null))
                return await Error(req, HttpStatusCode.BadRequest,
                "Provide at least one field to update: 'price' or 'isAvailable'.");
            if (dto.Price is not null && dto.Price < 0)
                return await Error(req, HttpStatusCode.BadRequest, "Price cannot be negative.");
            MenuItem existing;
            try
            {
                var getResponse = await _tableClient.GetEntityAsync<MenuItem>(category, id);
                existing = getResponse.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return await Error(req, HttpStatusCode.NotFound,
                $"Menu item '{id}' not found in category '{category}'.");
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Table Storage error while fetching menu item to update.");
                return await Error(req, HttpStatusCode.InternalServerError, "Failed to retrieve the menu item.");
            }
            if (dto.Price is not null) existing.Price = dto.Price.Value;
            if (dto.IsAvailable is not null) existing.IsAvailable = dto.IsAvailable.Value;
            try
            {
                await _tableClient.UpdateEntityAsync(existing, existing.ETag, TableUpdateMode.Replace);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Table Storage error while updating menu item.");
                return await Error(req, HttpStatusCode.InternalServerError, "Failed to update the menu item.");
            }
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(MenuItemResponseDto.FromEntity(existing));
            return response;
        }
        
        // DELETE /api/menu/{category}/{id} -> remove a menu item:
        
        [Function("DeleteMenuItem")]
        public async Task<HttpResponseData> DeleteMenuItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "menu/{category}/{id}")] HttpRequestData req,
        string category, string id)
        {
            _logger.LogInformation("Deleting menu item {Category}/{Id}.", category, id);
            try
            {
                await _tableClient.GetEntityAsync<MenuItem>(category, id);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return await Error(req, HttpStatusCode.NotFound,
                $"Menu item '{id}' not found in category '{category}'.");
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Table Storage error while checking menu item before delete.");
                return await Error(req, HttpStatusCode.InternalServerError, "Failed to look up the menu item.");
            }
            try
            {
                await _tableClient.DeleteEntityAsync(category, id);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Table Storage error while deleting menu item.");
                return await Error(req, HttpStatusCode.InternalServerError, "Failed to delete the menu item.");
            }
            return req.CreateResponse(HttpStatusCode.NoContent);
        }
       
        // Helpers:
        
        private static string? ValidateCreateDto(MenuItemCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Category)) return "Category is required.";
            if (string.IsNullOrWhiteSpace(dto.Sku)) return "Sku is required.";
            if (string.IsNullOrWhiteSpace(dto.Name)) return "Name is required.";
            if (dto.Price < 0) return "Price cannot be negative.";
            return null;
        }
        private static async Task<HttpResponseData> Error(HttpRequestData req, HttpStatusCode statusCode, string message)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(new { error = message });
            return response;
        }
    }
}