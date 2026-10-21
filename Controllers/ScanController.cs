using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;
using SmartScan.Services;

namespace SmartScan.Controllers
{
    // 1. A simple class to catch the Base64 image string from Javascript
    public class FrameRequest
    {
        public string Image { get; set; }
    }

    [Authorize(Roles = "Cashier")]
    [Route("api/[controller]")]
    [ApiController]
    public class ScanController : ControllerBase
    {
        private readonly YoloScannerService _scannerService;
        private readonly ApplicationDbContext _context;

        // Inject the database context alongside the AI service
        public ScanController(YoloScannerService scannerService, ApplicationDbContext context)
        {
            _scannerService = scannerService;
            _context = context;
        }

        // ==========================================
        // NEW ENDPOINT: Processes Live Webcam Frames
        // ==========================================
        [HttpPost("detect")]
        public IActionResult DetectFrame([FromBody] FrameRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Image))
            {
                return BadRequest("No image provided.");
            }

            try
            {
                // 1. Fetch only products assigned specifically to Store #11 (Kiervy's Sari-Sari)
                // Normalize spaces to underscores to match AI detection keys
                var activeStoreProducts = _context.Inventories
                    .Include(i => i.Product)
                    .Where(i => i.RetailId == 11)
                    .Select(i => i.Product.ProductName.ToLower().Replace(" ", "_").Replace("'", "''").Trim())
                    .ToList();

                // 2. Send the image AND the store inventory list to the YOLO service
                var detections = _scannerService.DetectObjects(request.Image, activeStoreProducts);

                // Return the filtered boxes to JavaScript to draw on the screen
                return Ok(new { detections = detections });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI Error] {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==========================================
        // OLD ENDPOINT: Kept safe for backward compatibility
        // ==========================================
        [HttpPost("receive")]
        public IActionResult ReceiveScanData([FromBody] List<ScannedItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return BadRequest("No items were scanned.");
            }

            foreach (var item in items)
            {
                System.Diagnostics.Debug.WriteLine($"Received: {item.ProductName} | Qty: {item.Quantity}");
            }

            return Ok(new { message = "Scan received successfully by ASP.NET!" });
        }
    }
}