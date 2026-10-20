using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public ScanController(YoloScannerService scannerService)
        {
            _scannerService = scannerService;
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
                // Send the image to our YOLO service and get the bounding boxes back
                var detections = _scannerService.DetectObjects(request.Image);

                // Return the boxes to JavaScript to draw on the screen
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