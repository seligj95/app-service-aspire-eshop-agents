using System.Collections.Generic;
using System.Text.Json.Serialization;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace dotnetfashionassistant.Models
{
    // External API model structure
    public class ExternalInventoryItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("name")]
        public required string Name { get; set; }
        
        [JsonPropertyName("category")]
        public required string Category { get; set; }
        
        [JsonPropertyName("price")]
        public decimal Price { get; set; }
        
        [JsonPropertyName("description")]
        public required string Description { get; set; }
        
        [JsonPropertyName("sizes")]
        public Dictionary<string, int> Sizes { get; set; } = new();
    }

    // Internal model structure (keeping existing structure for compatibility)
    public class InventoryItem
    {
        public int ProductId { get; set; }
        public required string ProductName { get; set; }
        public Dictionary<string, int> SizeInventory { get; set; }
        public decimal Price { get; set; }

        public InventoryItem()
        {
            SizeInventory = new Dictionary<string, int>();
            Price = 29.99m; // Default price if not specified
        }
    }

    public class InventoryService
    {
        private static readonly List<string> Sizes = new List<string> { "XS", "S", "M", "L", "XL", "XXL", "XXXL" };
        private readonly HttpClient _httpClient;
        private readonly string _externalApiUrl;

        public InventoryService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _externalApiUrl = configuration["EXTERNAL_INVENTORY_API_URL"] 
                             ?? "https://your-inventory-api.azurewebsites.net";
        }

        public async Task<List<InventoryItem>> GetInventoryAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_externalApiUrl}/api/inventory");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var externalItems = System.Text.Json.JsonSerializer.Deserialize<List<ExternalInventoryItem>>(json);
                
                // Map external API response to internal model
                return externalItems?.Select(MapToInventoryItem).ToList() ?? new List<InventoryItem>();
            }
            catch (Exception ex)
            {
                // Log error and provide helpful message for demo app
                Console.WriteLine($"Error fetching external inventory: {ex.Message}");
                Console.WriteLine($"Please configure the EXTERNAL_INVENTORY_API_URL app setting with a valid inventory API URL.");
                Console.WriteLine($"Current URL: {_externalApiUrl}");
                
                // Return empty list - no fallback for demo app
                return new List<InventoryItem>();
            }
        }

        public List<InventoryItem> GetInventory()
        {
            // Synchronous wrapper - in a real app, this should be avoided
            return GetInventoryAsync().GetAwaiter().GetResult();
        }

        private static InventoryItem MapToInventoryItem(ExternalInventoryItem external)
        {
            return new InventoryItem
            {
                ProductId = external.Id,
                ProductName = external.Name,
                Price = external.Price,
                SizeInventory = MapSizes(external.Sizes)
            };
        }

        private static Dictionary<string, int> MapSizes(Dictionary<string, int> externalSizes)
        {
            var sizeInventory = new Dictionary<string, int>();
            
            // Initialize all standard sizes with 0
            foreach (var size in Sizes)
            {
                sizeInventory[size] = 0;
            }
            
            // Map from external sizes to our standard sizes
            foreach (var kvp in externalSizes)
            {
                var standardSize = kvp.Key.ToUpper();
                if (Sizes.Contains(standardSize))
                {
                    sizeInventory[standardSize] = kvp.Value;
                }
            }
            
            return sizeInventory;
        }
        
        public static List<string> GetSizes()
        {
            return Sizes;
        }

        public List<string> GetAvailableSizes()
        {
            return Sizes;
        }
    }
}
