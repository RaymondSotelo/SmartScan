using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartScan.Models;
// Make sure to include your Models namespace if ScannedItem is in the Models folder
// using SmartScan.Models; 

namespace SmartScan.Controllers
{
    [Authorize(Roles = "Cashier")]
    [Route("api/[controller]")]
    [ApiController]
    public class ScanController : ControllerBase
    {
        [HttpPost("receive")]
        public IActionResult ReceiveScanData([FromBody] List<ScannedItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return BadRequest("No items were scanned.");
            }

            // Print the incoming items to the Visual Studio Output window to test the connection
            foreach (var item in items)
            {
                System.Diagnostics.Debug.WriteLine($"Received: {item.ProductName} | Qty: {item.Quantity}");
            }

            return Ok(new { message = "Scan received successfully by ASP.NET!" });
        }
    }
}