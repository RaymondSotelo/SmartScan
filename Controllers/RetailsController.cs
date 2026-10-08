using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Build.Tasks.Deployment.Bootstrapper;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartScan.Data;
using SmartScan.Models;
using SmartScan.Models.ViewModels;

[Authorize(Roles = "Admin")]
public class RetailsController : Controller
{
    private readonly ApplicationDbContext _context;

    public RetailsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: RETAILS
    public async Task<IActionResult> Index(string search)    
    {
        ViewData["CurrentFilter"] = search;

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Challenge();
        }

        var retailQuery = _context.Retails
            .Include(r => r.Status)
            .Include(r => r.AdminOwnerNavigation)
            .Where(r => r.AdminOwner == currentUserId) // Filter by logged-in admin
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            retailQuery = retailQuery.Where(r =>
                r.RetailName.Contains(search) ||
                r.Address.Contains(search));
        }

        return View(await retailQuery.ToListAsync());
    }

    // GET: RETAILS/View/5 or RETAILS/View?retailid=5
    [HttpGet]
    public async Task<IActionResult> View(int? retailid, int? id)
    {
        var targetId = retailid ?? id;

        if (targetId == null)
        {
            return NotFound();
        }

        //Identify current logged in user
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Challenge();
        }

        var retail = await _context.Retails
            .Include(r => r.Status)
            .Include(r => r.AdminOwnerNavigation)
            .Include(r => r.Inventories)
            .Include(r => r.RetailStaffs)
            .FirstOrDefaultAsync(r => r.RetailId == targetId && r.AdminOwner == currentUserId);

        if (retail == null)
        {
            return NotFound();
        }

        // Pass store details to ViewData for _RetailLayout.cshtml header & nav
        ViewData["RetailId"] = retail.RetailId;
        ViewData["RetailName"] = retail.RetailName;

        return RedirectToAction("Index", "Inventories", new { retailid = targetId });
    }

    // GET: RETAILS/Create
    public IActionResult Create()
    {
        ViewData["StatusId"] = new SelectList(_context.Statuses, "StatusId", "StatusName");
        ViewData["AdminOwner"] = new SelectList(_context.Users, "Id", "UserName");
        return View();
    }

    // POST: RETAILS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("RetailName,Address,Description")] Retail retail, IFormFile? retailImageFile, [FromServices] IWebHostEnvironment environment)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(currentUserId))
        {
            return Challenge(); // Prompts login if session expired
        }

        ModelState.Remove("AdminOwnerId");
        ModelState.Remove("AdminOwner");
        ModelState.Remove("Status");
        ModelState.Remove("StatusId");
        ModelState.Remove("ImagePath");

        retail.AdminOwner = currentUserId;
        retail.StatusId = 2;

        // Handle image upload
        if (retailImageFile != null && retailImageFile.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(retailImageFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("ImagePath", "Please upload a valid image file (.jpg, .jpeg, .png, .webp).");
            }
            else
            {
                var uploadDir = Path.Combine(environment.WebRootPath, "uploads", "retails");
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                // Create unique file name to avoid collision
                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadDir, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await retailImageFile.CopyToAsync(stream);
                }

                // Relative path stored in database
                retail.ImagePath = $"/uploads/retails/{uniqueFileName}";
            }           
        }
        else
        {
            // Set default image path if no image is uploaded
            retail.ImagePath = "/images/default-retail-pic.png";
        }

        if (ModelState.IsValid)
        {
            _context.Add(retail);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        return View(retail);
    }

    // GET: RETAILS/Edit/5
    public async Task<IActionResult> Edit(int? retailid)
    {
        if (retailid == null)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Challenge();
        }

        var retail = await _context.Retails
            .FirstOrDefaultAsync(r => r.RetailId == retailid && r.AdminOwner == currentUserId);

        if (retail == null)
        {
            return NotFound();
        }

        ViewData["StatusId"] = new SelectList(_context.Statuses, "StatusId", "StatusName");
        return View(retail);
    }

    // POST: RETAILS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
    int retailid,
    [Bind("RetailId,RetailName,Address,Description,StatusId")] Retail retail,
    IFormFile? retailImageFile,
    [FromServices] IWebHostEnvironment environment)
    {
        if (retailid != retail.RetailId)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Challenge();
        }

        var existingRetail = await _context.Retails
            .FirstOrDefaultAsync(r => r.RetailId == retailid && r.AdminOwner == currentUserId);

        if (existingRetail == null)
        {
            return NotFound();
        }

        ModelState.Remove("AdminOwnerId");
        ModelState.Remove("AdminOwner");
        ModelState.Remove("Status");
        ModelState.Remove("StatusId");
        ModelState.Remove("ImagePath");

        // Validate uploaded file extension before touching the disk
        string? extension = null;
        if (retailImageFile != null && retailImageFile.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            extension = Path.GetExtension(retailImageFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("ImagePath", "Please upload a valid image file (.jpg, .jpeg, .png, .webp).");
            }
        }

        if (ModelState.IsValid)
        {
            // Handle file write and old file cleanup only when form is valid
            if (retailImageFile != null && retailImageFile.Length > 0 && extension != null)
            {
                var uploadDir = Path.Combine(environment.WebRootPath, "uploads", "retails");
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                // Remove previous image from disk if one exists
                if (!string.IsNullOrEmpty(existingRetail.ImagePath))
                {
                    var oldFilePath = Path.Combine(environment.WebRootPath, existingRetail.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var newFilePath = Path.Combine(uploadDir, uniqueFileName);

                using (var stream = new FileStream(newFilePath, FileMode.Create))
                {
                    await retailImageFile.CopyToAsync(stream);
                }

                existingRetail.ImagePath = $"/uploads/retails/{uniqueFileName}";
            }

            existingRetail.RetailName = retail.RetailName;
            existingRetail.Address = retail.Address;
            existingRetail.Description = retail.Description;
            existingRetail.StatusId = retail.StatusId;

            try
            {
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Retails.Any(r => r.RetailId == retailid))
                {
                    return NotFound();
                }
                throw;
            }
        }

        // Preserve existing image path for view re-render
        retail.ImagePath = existingRetail.ImagePath;
        return View(retail);
    }

    // GET: RETAILS/Delete/5
    public async Task<IActionResult> Delete(int? retailid)
    {
        if (retailid == null)
        {
            return NotFound();
        }

        var retail = await _context.Retails
            .FirstOrDefaultAsync(m => m.RetailId == retailid);
        if (retail == null)
        {
            return NotFound();
        }

        return View(retail);
    }

    // POST: RETAILS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? retailid, int? id, [FromServices] IWebHostEnvironment environment)
    {
        var targetId = retailid ?? id;
        if (targetId == null)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Challenge();
        }

        var retail = await _context.Retails
            .FirstOrDefaultAsync(r => r.RetailId == targetId && r.AdminOwner == currentUserId);

        if (retail == null)
        {
            return NotFound();
        }

        try
        {
            if (!string.IsNullOrEmpty(retail.ImagePath) && !retail.ImagePath.Contains("default-retail-pic.png"))
            {
                var relativePath = retail.ImagePath.TrimStart('/');
                var physicalPath = Path.Combine(environment.WebRootPath, relativePath);

                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                }
            }

            _context.Retails.Remove(retail);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Retail deleted successfully.";
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Cannot delete this retail because there is an existing inventory or staff attached to it.";
        }

        return RedirectToAction(nameof(Index));
    }

    //GET: Sales Analysis
    public async Task<IActionResult> SalesAnalysis(int retailid)
    {
        var today = DateTime.Today;
        var sevenDaysAgo = today.AddDays(-6);

        // Base queries filtered by Retail
        var transactionsQuery = _context.Transactions
            .Where(t => t.RetailId == retailid && t.StatusId == 1); // Assuming 1 = Completed/Active

        var itemsQuery = _context.TransactionItems
            .Include(ti => ti.Product) // Assuming navigation property exists
            .Where(ti => ti.Transaction.RetailId == retailid && ti.Transaction.StatusId == 1);

        var model = new SalesAnalysisViewModel();

        // 1. KPI Metrics
        model.TodayTransactions = await transactionsQuery
            .Where(t => t.CreatedAt.Date == today)
            .CountAsync();

        model.TotalRevenue = await transactionsQuery.SumAsync(t => t.TotalAmount);
        model.TotalItemsSold = await itemsQuery.SumAsync(ti => ti.Quantity);

        // 2. Top Selling Product (All Time)
        var topProduct = await itemsQuery
            .GroupBy(ti => new { ti.ProductId, ti.Product.ProductName }) // Adjust 'ProductName' to match your model
            .Select(g => new { Name = g.Key.ProductName, TotalQty = g.Sum(ti => ti.Quantity) })
            .OrderByDescending(x => x.TotalQty)
            .FirstOrDefaultAsync();

        if (topProduct != null) model.TopSellingItemName = topProduct.Name;

        // 3. Graph 1: Revenue Last 7 Days
        var dailyRevenue = await transactionsQuery
            .Where(t => t.CreatedAt >= sevenDaysAgo)
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Revenue = g.Sum(t => t.TotalAmount) })
            .ToListAsync();

        // Fill in missing days with 0
        var last7Days = Enumerable.Range(0, 7)
            .Select(offset => sevenDaysAgo.AddDays(offset))
            .Select(date => new {
                DateString = date.ToString("MMM dd"),
                Revenue = dailyRevenue.FirstOrDefault(d => d.Date == date)?.Revenue ?? 0
            }).ToList();

        model.RevenueDatesJson = JsonSerializer.Serialize(last7Days.Select(d => d.DateString));
        model.RevenueValuesJson = JsonSerializer.Serialize(last7Days.Select(d => d.Revenue));

        // 4. Graph 2: Top 5 Products by Quantity
        var top5Products = await itemsQuery
            .GroupBy(ti => ti.Product.ProductName)
            .Select(g => new { Name = g.Key, Qty = g.Sum(ti => ti.Quantity) })
            .OrderByDescending(x => x.Qty)
            .Take(5)
            .ToListAsync();

        model.TopProductNamesJson = JsonSerializer.Serialize(top5Products.Select(p => p.Name));
        model.TopProductQuantitiesJson = JsonSerializer.Serialize(top5Products.Select(p => p.Qty));

        // 5. Graph 3: Payment Methods
        var paymentMethods = await transactionsQuery
            .Include(t => t.PaymentMethod) // Assuming navigation property exists
            .GroupBy(t => t.PaymentMethod.PaymentMethodName) // Adjust property name
            .Select(g => new { Method = g.Key, Count = g.Count() })
            .ToListAsync();

        model.PaymentMethodLabelsJson = JsonSerializer.Serialize(paymentMethods.Select(p => p.Method));
        model.PaymentMethodCountsJson = JsonSerializer.Serialize(paymentMethods.Select(p => p.Count));

        ViewBag.RetailId = retailid;
        return View(model);
    }

    // GET: Retails/ExportExcel?retailId=1
    [HttpGet]
    public async Task<IActionResult> ExportExcel(int retailId)
    {
        var transactions = await _context.Transactions
            .Include(t => t.PaymentMethod)
            .Include(t => t.Status)
            .Include(t => t.RetailStaff).ThenInclude(rs => rs.Cashier)
            .Include(t => t.TransactionItems).ThenInclude(ti => ti.Product)
            .Where(t => t.RetailId == retailId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        using var workbook = new XLWorkbook();

        // --- SHEET 1: TRANSACTIONS ---
        var ws1 = workbook.Worksheets.Add("Transactions");
        ws1.Cell(1, 1).Value = "Store Sales Report - Transactions";
        ws1.Cell(1, 1).Style.Font.Bold = true;
        ws1.Cell(1, 1).Style.Font.FontSize = 14;

        string[] h1 = { "Receipt #", "Date", "Cashier", "Payment Method", "Items Count", "Status", "Total Amount" };
        for (int i = 0; i < h1.Length; i++)
        {
            var cell = ws1.Cell(3, i + 1);
            cell.Value = h1[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1162A3");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row1 = 4;
        foreach (var t in transactions)
        {
            ws1.Cell(row1, 1).Value = t.ReceiptNumber;
            ws1.Cell(row1, 2).Value = t.CreatedAt.ToString("yyyy-MM-dd HH:mm");
            ws1.Cell(row1, 3).Value = t.RetailStaff?.Cashier?.UserName ?? "N/A";
            ws1.Cell(row1, 4).Value = t.PaymentMethod?.PaymentMethodName ?? "N/A";
            ws1.Cell(row1, 5).Value = t.TransactionItems.Sum(ti => ti.Quantity);
            ws1.Cell(row1, 6).Value = t.Status?.StatusName ?? "Completed";
            ws1.Cell(row1, 7).Value = t.TotalAmount;
            ws1.Cell(row1, 7).Style.NumberFormat.Format = "₱#,##0.00";
            row1++;
        }
        ws1.Columns().AdjustToContents();

        // --- SHEET 2: TRANSACTION ITEMS ---
        var ws2 = workbook.Worksheets.Add("Transaction Items");
        ws2.Cell(1, 1).Value = "Store Sales Report - Item Details";
        ws2.Cell(1, 1).Style.Font.Bold = true;
        ws2.Cell(1, 1).Style.Font.FontSize = 14;

        string[] h2 = { "Receipt #", "Date", "Product Name", "Unit Price", "Quantity", "Subtotal" };
        for (int i = 0; i < h2.Length; i++)
        {
            var cell = ws2.Cell(3, i + 1);
            cell.Value = h2[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#198754"); // Green header for distinction
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row2 = 4;
        foreach (var t in transactions)
        {
            foreach (var item in t.TransactionItems)
            {
                ws2.Cell(row2, 1).Value = t.ReceiptNumber;
                ws2.Cell(row2, 2).Value = t.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                ws2.Cell(row2, 3).Value = item.Product?.ProductName ?? "Unknown";

                ws2.Cell(row2, 4).Value = item.UnitPrice;
                ws2.Cell(row2, 4).Style.NumberFormat.Format = "₱#,##0.00";

                ws2.Cell(row2, 5).Value = item.Quantity;

                ws2.Cell(row2, 6).Value = item.UnitPrice * item.Quantity;
                ws2.Cell(row2, 6).Style.NumberFormat.Format = "₱#,##0.00";
                row2++;
            }
        }
        ws2.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"SalesReport_Store_{retailId}_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    // POST: Retails/ExportPdf
    [HttpPost]
    public async Task<IActionResult> ExportPdf(int retailId, string revenueChartImg, string productsChartImg, string paymentChartImg, [FromServices] IWebHostEnvironment env)
    {
        var transactions = await _context.Transactions
            .Include(t => t.PaymentMethod)
            .Include(t => t.RetailStaff).ThenInclude(rs => rs.Cashier)
            .Include(t => t.TransactionItems)
            .Where(t => t.RetailId == retailId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var store = await _context.Retails.FindAsync(retailId);
        var totalRevenue = transactions.Sum(t => t.TotalAmount);

        // Read Logo Bytes
        var logoPath = Path.Combine(env.WebRootPath, "images", "SmartScanLogo.png");
        byte[] logoBytes = System.IO.File.Exists(logoPath) ? await System.IO.File.ReadAllBytesAsync(logoPath) : Array.Empty<byte>();

        // Decode Chart Images from Base64
        byte[] revChartBytes = !string.IsNullOrEmpty(revenueChartImg) ? Convert.FromBase64String(revenueChartImg.Split(',')[1]) : Array.Empty<byte>();
        byte[] prodChartBytes = !string.IsNullOrEmpty(productsChartImg) ? Convert.FromBase64String(productsChartImg.Split(',')[1]) : Array.Empty<byte>();
        byte[] payChartBytes = !string.IsNullOrEmpty(paymentChartImg) ? Convert.FromBase64String(paymentChartImg.Split(',')[1]) : Array.Empty<byte>();

        var pdfDocument = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(35);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Lato));

                // 1. PROFESSIONAL HEADER
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        // Logo
                        if (logoBytes.Length > 0)
                            row.ConstantItem(60).Height(60).Image(logoBytes);

                        // Capstone Title & Retail Details
                        row.RelativeItem().PaddingLeft(10).Column(c =>
                        {
                            c.Item().Text("SMARTSCAN: A Vision-Basedd Automated Checkout For Retail Counters")
                                .Bold().FontSize(12).FontColor("#1162A3");

                            c.Item().PaddingTop(4).Text($"Retail Partner: {store?.RetailName ?? "N/A"}")
                                .Bold().FontSize(16).FontColor(Colors.Black);

                            c.Item().Text($"Report Generated: {DateTime.Now:MMMM dd, yyyy - hh:mm tt}")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        });

                        // KPI Block
                        row.ConstantItem(120).AlignRight().Column(c => {
                            c.Item().Text("TOTAL REVENUE").FontSize(8).FontColor(Colors.Grey.Medium);
                            c.Item().Text($"₱{totalRevenue:N2}").Bold().FontSize(16).FontColor("#198754");
                        });
                    });
                    col.Item().PaddingVertical(10).LineHorizontal(1.5f).LineColor(Colors.Grey.Lighten2);
                });

                // 2. CONTENT (Charts & Tables)
                page.Content().Column(col =>
                {
                    // Graphs Section
                    if (revChartBytes.Length > 0 || prodChartBytes.Length > 0)
                    {
                        col.Item().PaddingBottom(10).Text("Sales Visualizations").Bold().FontSize(12).FontColor("#1162A3");

                        col.Item().Row(row =>
                        {
                            // Revenue Chart (Takes up 2/3 width)
                            if (revChartBytes.Length > 0)
                                row.RelativeItem(2).PaddingRight(10).Image(revChartBytes);

                            // Payment Chart (Takes up 1/3 width)
                            if (payChartBytes.Length > 0)
                                row.RelativeItem(1).Image(payChartBytes);
                        });

                        col.Item().PaddingVertical(10).Row(row =>
                        {
                            // Product Bar Chart (Full width)
                            if (prodChartBytes.Length > 0)
                                row.RelativeItem().Image(prodChartBytes);
                        });

                        col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten3);
                    }

                    col.Item().PaddingBottom(5).Text("Recent Transactions").Bold().FontSize(12).FontColor("#1162A3");

                    // Table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2); // Receipt
                            cols.RelativeColumn(2); // Date
                            cols.RelativeColumn(2); // Cashier
                            cols.RelativeColumn(2); // Payment
                            cols.RelativeColumn(1); // Qty
                            cols.RelativeColumn(2); // Amount
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background("#1162A3").Padding(5).Text("Receipt #").FontColor(Colors.White).Bold();
                            h.Cell().Background("#1162A3").Padding(5).Text("Date").FontColor(Colors.White).Bold();
                            h.Cell().Background("#1162A3").Padding(5).Text("Cashier").FontColor(Colors.White).Bold();
                            h.Cell().Background("#1162A3").Padding(5).Text("Method").FontColor(Colors.White).Bold();
                            h.Cell().Background("#1162A3").Padding(5).Text("Qty").FontColor(Colors.White).Bold();
                            h.Cell().Background("#1162A3").Padding(5).AlignRight().Text("Amount").FontColor(Colors.White).Bold();
                        });

                        foreach (var t in transactions.Take(50)) // Cap at 50 for the PDF preview so it doesn't get massive
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(t.ReceiptNumber);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(t.CreatedAt.ToString("MM/dd/yyyy"));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(t.RetailStaff?.Cashier?.UserName ?? "N/A");
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(t.PaymentMethod?.PaymentMethodName ?? "N/A");
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(t.TransactionItems.Sum(x => x.Quantity).ToString());
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"₱{t.TotalAmount:N2}");
                        }
                    });
                });

                // 3. FOOTER
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        });

        return File(pdfDocument.GeneratePdf(), "application/pdf", $"SalesReport_{store?.RetailName}_{DateTime.Now:yyyyMMdd}.pdf");
    }

    private bool RetailExists(int? retailid)
    {
        return _context.Retails.Any(e => e.RetailId == retailid);
    }
}
