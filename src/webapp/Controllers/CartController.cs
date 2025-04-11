using Microsoft.AspNetCore.Mvc;
using dotnetfashionassistant.Models;
using System.Collections.Generic;
using System.Linq;

namespace dotnetfashionassistant.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CartController : ControllerBase
    {
        /// <summary>
        /// Gets all items in the shopping cart
        /// </summary>
        /// <returns>A list of all cart items</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CartItem>), 200)]
        public ActionResult<IEnumerable<CartItem>> GetCart()
        {
            return Ok(CartService.GetCart());
        }

        /// <summary>
        /// Adds an item to the shopping cart
        /// </summary>
        /// <param name="request">The item to add to the cart</param>
        /// <returns>The updated cart</returns>
        [HttpPost("add")]
        [ProducesResponseType(typeof(IEnumerable<CartItem>), 200)]
        [ProducesResponseType(400)]
        public ActionResult<IEnumerable<CartItem>> AddToCart(AddToCartRequest request)
        {
            // Validate request
            if (request.Quantity <= 0)
            {
                return BadRequest("Quantity must be greater than zero");
            }

            // Check if the product exists and is in stock
            var product = InventoryService.GetInventory().FirstOrDefault(i => i.ProductId == request.ProductId);
            if (product == null)
            {
                return BadRequest("Product not found");
            }

            if (!product.SizeInventory.TryGetValue(request.Size, out int stock))
            {
                return BadRequest($"Size {request.Size} not available for this product");
            }

            if (stock < request.Quantity)
            {
                return BadRequest($"Not enough stock. Only {stock} items available");
            }

            // Add to cart
            CartService.AddToCart(request.ProductId, product.ProductName, request.Size, request.Quantity);
            
            return Ok(CartService.GetCart());
        }

        /// <summary>
        /// Updates the quantity of an item in the cart
        /// </summary>
        /// <param name="productId">The product ID</param>
        /// <param name="size">The product size</param>
        /// <param name="request">The update quantity request</param>
        /// <returns>The updated cart</returns>
        [HttpPut("{productId}/size/{size}")]
        [ProducesResponseType(typeof(IEnumerable<CartItem>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public ActionResult<IEnumerable<CartItem>> UpdateCartItemQuantity(int productId, string size, UpdateQuantityRequest request)
        {
            // Check if trying to update to a positive quantity
            if (request.Quantity > 0)
            {
                // Check if the product exists and has enough stock
                var product = InventoryService.GetInventory().FirstOrDefault(i => i.ProductId == productId);
                if (product == null)
                {
                    return BadRequest("Product not found");
                }

                if (!product.SizeInventory.TryGetValue(size, out int stock))
                {
                    return BadRequest($"Size {size} not available for this product");
                }

                if (stock < request.Quantity)
                {
                    return BadRequest($"Not enough stock. Only {stock} items available");
                }
            }

            // Update the cart
            bool success = CartService.UpdateCartItemQuantity(productId, size, request.Quantity);
            if (!success)
            {
                return NotFound("Item not found in cart");
            }
            
            return Ok(CartService.GetCart());
        }

        /// <summary>
        /// Removes an item from the cart
        /// </summary>
        /// <param name="productId">The product ID</param>
        /// <param name="size">The product size</param>
        /// <returns>The updated cart</returns>
        [HttpDelete("{productId}/size/{size}")]
        [ProducesResponseType(typeof(IEnumerable<CartItem>), 200)]
        [ProducesResponseType(404)]
        public ActionResult<IEnumerable<CartItem>> RemoveFromCart(int productId, string size)
        {
            bool success = CartService.RemoveFromCart(productId, size);
            if (!success)
            {
                return NotFound("Item not found in cart");
            }
            
            return Ok(CartService.GetCart());
        }

        /// <summary>
        /// Clears all items from the cart
        /// </summary>
        /// <returns>Empty cart</returns>
        [HttpDelete]
        [ProducesResponseType(typeof(IEnumerable<CartItem>), 200)]
        public ActionResult<IEnumerable<CartItem>> ClearCart()
        {
            CartService.ClearCart();
            return Ok(CartService.GetCart());
        }
    }

    public class AddToCartRequest
    {
        /// <summary>
        /// The product ID to add to the cart
        /// </summary>
        public int ProductId { get; set; }
        
        /// <summary>
        /// The size of the product (e.g., XS, S, M, L, XL, XXL, XXXL)
        /// </summary>
        public required string Size { get; set; }
        
        /// <summary>
        /// The quantity to add
        /// </summary>
        public int Quantity { get; set; }
    }

    public class UpdateQuantityRequest
    {
        /// <summary>
        /// The new quantity for the cart item
        /// </summary>
        public int Quantity { get; set; }
    }
}
