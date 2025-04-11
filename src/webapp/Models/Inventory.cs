using System.Collections.Generic;

namespace dotnetfashionassistant.Models
{    public class InventoryItem
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

    public static class InventoryService
    {
        private static readonly List<string> Sizes = new List<string> { "XS", "S", "M", "L", "XL", "XXL", "XXXL" };

        public static List<InventoryItem> GetInventory()
        {
            // Create dummy inventory data
            var inventory = new List<InventoryItem>();
              // Navy Formal Blazer
            var blazer = new InventoryItem
            {
                ProductId = 3,
                ProductName = "Navy Single-Breasted Slim Fit Formal Blazer",
                Price = 89.99m,
                SizeInventory = new Dictionary<string, int>
                {
                    { "XS", 0 },
                    { "S", 0 },
                    { "M", 0 },
                    { "L", 0 },
                    { "XL", 0 },
                    { "XXL", 0 },
                    { "XXXL", 0 }
                }
            };
              // White & Navy Blue Shirt
            var whiteNavyShirt = new InventoryItem
            {
                ProductId = 111,
                ProductName = "White & Navy Blue Slim Fit Printed Casual Shirt",
                Price = 34.99m,
                SizeInventory = new Dictionary<string, int>
                {
                    { "XS", 8 },
                    { "S", 15 },
                    { "M", 0 },
                    { "L", 18 },
                    { "XL", 12 },
                    { "XXL", 0 },
                    { "XXXL", 4 }
                }
            };
              // Red Checked Shirt
            var redShirt = new InventoryItem
            {
                ProductId = 116,
                ProductName = "Red Slim Fit Checked Casual Shirt",
                Price = 39.99m,
                SizeInventory = new Dictionary<string, int>
                {
                    { "XS", 10 },
                    { "S", 18 },
                    { "M", 22 },
                    { "L", 16 },
                    { "XL", 14 },
                    { "XXL", 7 },
                    { "XXXL", 5 }
                }
            };
              // Navy Blue Denim Jacket
            var denimJacket = new InventoryItem
            {
                ProductId = 10,
                ProductName = "Navy Blue Washed Denim Jacket",
                Price = 59.99m,
                SizeInventory = new Dictionary<string, int>
                {
                    { "XS", 6 },
                    { "S", 14 },
                    { "M", 19 },
                    { "L", 17 },
                    { "XL", 11 },
                    { "XXL", 9 },
                    { "XXXL", 4 }
                }
            };
            
            inventory.Add(blazer);
            inventory.Add(whiteNavyShirt);
            inventory.Add(redShirt);
            inventory.Add(denimJacket);
            
            return inventory;
        }
        
        public static List<string> GetSizes()
        {
            return Sizes;
        }
    }
}
