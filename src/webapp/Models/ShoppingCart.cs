using System.Collections.Generic;
using System.Linq;

namespace dotnetfashionassistant.Models
{
    public class CartItem
    {
        public int ProductId { get; set; }
        public required string ProductName { get; set; }
        public required string Size { get; set; }
        public int Quantity { get; set; }
    }

    public static class CartService
    {
        // In-memory cart storage - in a real application, this would use session state or a database
        private static readonly List<CartItem> Cart = new();

        /// <summary>
        /// Gets all items currently in the cart
        /// </summary>
        public static List<CartItem> GetCart()
        {
            return Cart;
        }

        /// <summary>
        /// Adds an item to the cart or increases quantity if it already exists
        /// </summary>
        public static void AddToCart(int productId, string productName, string size, int quantity)
        {
            // Check if the item already exists in the cart with the same product ID and size
            var existingItem = Cart.FirstOrDefault(item => item.ProductId == productId && item.Size == size);
            
            if (existingItem != null)
            {
                // If item exists, just update the quantity
                existingItem.Quantity += quantity;
            }
            else
            {
                // Add new item to the cart
                Cart.Add(new CartItem
                {
                    ProductId = productId,
                    ProductName = productName,
                    Size = size,
                    Quantity = quantity
                });
            }
        }

        /// <summary>
        /// Updates the quantity of an item in the cart
        /// </summary>
        public static bool UpdateCartItemQuantity(int productId, string size, int quantity)
        {
            var existingItem = Cart.FirstOrDefault(item => item.ProductId == productId && item.Size == size);
            
            if (existingItem == null)
            {
                return false;
            }

            if (quantity <= 0)
            {
                // If quantity is zero or negative, remove the item
                return RemoveFromCart(productId, size);
            }

            existingItem.Quantity = quantity;
            return true;
        }

        /// <summary>
        /// Removes an item from the cart
        /// </summary>
        public static bool RemoveFromCart(int productId, string size)
        {
            var existingItem = Cart.FirstOrDefault(item => item.ProductId == productId && item.Size == size);
            
            if (existingItem == null)
            {
                return false;
            }
            
            Cart.Remove(existingItem);
            return true;
        }

        /// <summary>
        /// Clears all items from the cart
        /// </summary>
        public static void ClearCart()
        {
            Cart.Clear();
        }
    }
}
