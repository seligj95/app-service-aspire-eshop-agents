using Microsoft.AspNetCore.Mvc;
using dotnetfashionassistant.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace dotnetfashionassistant.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class InventoryController : ControllerBase
    {
        private readonly InventoryService _inventoryService;

        public InventoryController(InventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        /// <summary>
        /// Gets all inventory items
        /// </summary>
        /// <returns>A list of all inventory items with their sizes and quantities</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<InventoryItem>), 200)]
        public async Task<ActionResult<IEnumerable<InventoryItem>>> GetInventory()
        {
            var inventory = await _inventoryService.GetInventoryAsync();
            return Ok(inventory);
        }

        /// <summary>
        /// Gets a specific inventory item by product ID
        /// </summary>
        /// <param name="id">The product ID to look up</param>
        /// <returns>The inventory item matching the specified ID</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(InventoryItem), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<InventoryItem>> GetInventoryItem(int id)
        {
            var inventory = await _inventoryService.GetInventoryAsync();
            var item = inventory.FirstOrDefault(i => i.ProductId == id);
            
            if (item == null)
            {
                return NotFound();
            }
            
            return Ok(item);
        }

        /// <summary>
        /// Gets inventory count for a specific product and size
        /// </summary>
        /// <param name="id">The product ID</param>
        /// <param name="size">The size to check (e.g., XS, S, M, L, XL, XXL, XXXL)</param>
        /// <returns>The inventory count for the specified product and size</returns>
        [HttpGet("{id}/size/{size}")]
        [ProducesResponseType(typeof(SizeInventoryResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<SizeInventoryResponse>> GetSizeInventory(int id, string size)
        {
            var inventory = await _inventoryService.GetInventoryAsync();
            var item = inventory.FirstOrDefault(i => i.ProductId == id);
            
            if (item == null)
            {
                return NotFound("Product not found");
            }

            if (!item.SizeInventory.TryGetValue(size.ToUpper(), out var count))
            {
                return NotFound($"Size {size} not found for product {id}");
            }
            
            return Ok(new SizeInventoryResponse 
            { 
                ProductId = id,
                ProductName = item.ProductName,
                Size = size.ToUpper(),
                Count = count
            });
        }

        /// <summary>
        /// Gets all available sizes
        /// </summary>
        /// <returns>A list of available sizes</returns>
        [HttpGet("sizes")]
        [ProducesResponseType(typeof(IEnumerable<string>), 200)]
        public ActionResult<IEnumerable<string>> GetSizes()
        {
            return Ok(InventoryService.GetSizes());
        }
    }    /// <summary>
    /// Represents the response for a size inventory query
    /// </summary>
    public class SizeInventoryResponse
    {
        /// <summary>
        /// The product ID
        /// </summary>
        public int ProductId { get; set; }
        
        /// <summary>
        /// The product name
        /// </summary>
        public required string ProductName { get; set; }
        
        /// <summary>
        /// The size being queried (e.g., XS, S, M, L, XL, XXL, XXXL)
        /// </summary>
        public required string Size { get; set; }
        
        /// <summary>
        /// The inventory count for the specified product and size
        /// </summary>
        public int Count { get; set; }
    }
}
