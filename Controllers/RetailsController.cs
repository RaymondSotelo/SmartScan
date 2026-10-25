using System.Security.Claims;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartScan.Data;
using SmartScan.Models;
using SmartScan.Models.ViewModels;

namespace SmartScan.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RetailsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public RetailsController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Retails
        public async Task<IActionResult> Index(string? search)
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
                .Include(r => r.RetailStaffs)
                .AsNoTracking()
                .Where(r =>
                    r.AdminOwner == currentUserId ||
                    r.RetailStaffs.Any(rs =>
                        rs.StaffId == currentUserId &&
                        rs.Status != null &&
                        rs.Status.StatusName == "Active"))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                retailQuery = retailQuery.Where(r =>
                    r.RetailName.Contains(search) ||
                    (r.Address != null && r.Address.Contains(search)));
            }

            return View(await retailQuery.ToListAsync());
        }

        // GET: Retails/View/5 or Retails/View?retailid=5
        [HttpGet]
        public async Task<IActionResult> View(int? retailid, int? id)
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
                .Include(r => r.Status)
                .Include(r => r.AdminOwnerNavigation)
                .Include(r => r.Inventories)
                .Include(r => r.RetailStaffs)
                .FirstOrDefaultAsync(r => r.RetailId == targetId);

            if (retail == null)
            {
                return NotFound();
            }

            // Security check: Store Owner vs Assigned Active Co-Admin
            bool isHeadAdmin = retail.AdminOwner == currentUserId;
            bool isCoAdmin = false;

            if (!isHeadAdmin)
            {
                bool isAssignedActiveStaff = retail.RetailStaffs.Any(rs => rs.StaffId == currentUserId && rs.StatusId == 1);
                if (isAssignedActiveStaff)
                {
                    var currentUser = await _userManager.FindByIdAsync(currentUserId);
                    if (currentUser != null && await _userManager.IsInRoleAsync(currentUser, "Admin"))
                    {
                        isCoAdmin = true;
                    }
                }
            }

            if (!isHeadAdmin && !isCoAdmin)
            {
                return Forbid();
            }

            ViewData["RetailId"] = retail.RetailId;
            ViewData["RetailName"] = retail.RetailName;

            return RedirectToAction("Index", "Inventories", new { retailid = targetId });
        }

        // GET: Retails/Create
        public IActionResult Create()
        {
            ViewData["StatusId"] = new SelectList(_context.Statuses, "StatusId", "StatusName");
            ViewData["AdminOwner"] = new SelectList(_context.Users, "Id", "UserName");
            return View();
        }

        // POST: Retails/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("RetailName,Address,Description")] Retail retail,
            IFormFile? retailImageFile,
            [FromServices] IWebHostEnvironment environment)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Challenge();
            }

            ModelState.Remove("AdminOwnerId");
            ModelState.Remove("AdminOwner");
            ModelState.Remove("Status");
            ModelState.Remove("StatusId");
            ModelState.Remove("ImagePath");

            retail.AdminOwner = currentUserId;
            retail.StatusId = 2; // Default Inactive until approved/setup

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
                    Directory.CreateDirectory(uploadDir);

                    var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadDir, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await retailImageFile.CopyToAsync(stream);
                    }

                    retail.ImagePath = $"/uploads/retails/{uniqueFileName}";
                }
            }
            else
            {
                retail.ImagePath = "/images/default-store.png";
            }

            if (ModelState.IsValid)
            {
                _context.Add(retail);
                await _context.SaveChangesAsync();
                TempData["RetailMessage"] = $"'{retail.RetailName}' was added successfully.";
                return RedirectToAction(nameof(Index));
            }

            return View(retail);
        }

        // GET: Retails/Edit/5
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

            // Checks if current store exist
            var retail = await _context.Retails.FindAsync(retailid);
            if (retail == null)
            {
                return NotFound();
            }

            if (retail.AdminOwner != currentUserId)
            {
                // Check if admin is assigned to a retail as a co-admin
                bool isCoAdmin = await _context.RetailStaffs
                    .AnyAsync(s => s.RetailId == retailid && s.StaffId == currentUserId);

                if (isCoAdmin)
                {
                    TempData["NoEntryMessage"] = "As a co-admin/staff member, you do not have permission to modify store settings. Only the primary store owner can edit this outlet.";
                }
                else
                {
                    TempData["NoEntryMessage"] = "You do not have permission to edit this retail branch.";
                }

                // Redirect back to previous page; fallback to Index if Referer is missing
                string? previousUrl = Request.Headers.Referer.ToString();
                if (!string.IsNullOrEmpty(previousUrl))
                {
                    return Redirect(previousUrl);
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["StatusId"] = new SelectList(_context.Statuses, "StatusId", "StatusName");
            return View(retail);
        }

        // POST: Retails/Edit/5
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

            string? extension = null;
            if (retailImageFile != null && retailImageFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                extension = Path.GetExtension(retailImageFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("ImagePath", "Please upload a valid image (.jpg, .jpeg, .png, .webp).");
                }
            }

            if (ModelState.IsValid)
            {
                if (retailImageFile != null && retailImageFile.Length > 0 && extension != null)
                {
                    var uploadDir = Path.Combine(environment.WebRootPath, "uploads", "retails");
                    Directory.CreateDirectory(uploadDir);

                    // Delete existing non-default image from disk
                    if (!string.IsNullOrEmpty(existingRetail.ImagePath) &&
                        !existingRetail.ImagePath.Contains("default-store.png") &&
                        !existingRetail.ImagePath.Contains("default-retail-pic.png"))
                    {
                        var oldPath = Path.Combine(environment.WebRootPath, existingRetail.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(oldPath))
                        {
                            System.IO.File.Delete(oldPath);
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
                    TempData["RetailMessage"] = $"'{retail.RetailName}' was updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RetailExists(retailid))
                    {
                        return NotFound();
                    }
                    throw;
                }
            }

            retail.ImagePath = existingRetail.ImagePath;
            return View(retail);
        }

        // GET: Retails/Delete/5
        public async Task<IActionResult> Delete(int? retailid)
        {
            if (retailid == null)
            {
                return NotFound();
            }

            var retail = await _context.Retails.FirstOrDefaultAsync(m => m.RetailId == retailid);
            if (retail == null)
            {
                return NotFound();
            }

            return View(retail);
        }

        // POST: Retails/Delete/5
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

            var imagePathToDelete = retail.ImagePath;

            try
            {
                _context.Retails.Remove(retail);
                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(imagePathToDelete) &&
                    !imagePathToDelete.Contains("default-retail-pic.png") &&
                    !imagePathToDelete.Contains("default-store.png"))
                {
                    var relativePath = imagePathToDelete.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var physicalPath = Path.Combine(environment.WebRootPath, relativePath);

                    if (System.IO.File.Exists(physicalPath))
                    {
                        System.IO.File.Delete(physicalPath);
                    }
                }

                TempData["RetailMessage"] = $"'{retail.RetailName}' was permanently deleted.";
            }
            catch (DbUpdateException)
            {
                _context.Entry(retail).State = EntityState.Unchanged;

                retail.StatusId = 2; // Inactive
                _context.Update(retail);
                await _context.SaveChangesAsync();

                TempData["AlertMessage"] = $"'{retail.RetailName}' could not be permanently deleted due to linked records (staff/inventory), so its status was set to Inactive instead.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Retails/SalesAnalysis
        public async Task<IActionResult> SalesAnalysis(int? retailId, int? id, string range = "7days")
        {
            var targetId = retailId ?? id;
            if (targetId == null || targetId == 0) return NotFound();

            var today = DateTime.Today;

            DateTime startDate = range switch
            {
                "1month" => today.AddMonths(-1),
                "6months" => today.AddMonths(-6),
                "1year" => today.AddYears(-1),
                _ => today.AddDays(-6)
            };

            string rangeLabel = range switch
            {
                "1month" => "Last Month",
                "6months" => "Last 6 Months",
                "1year" => "Last Year",
                _ => "Last 7 Days"
            };

            var transactionsQuery = _context.Transactions
                .Where(t => t.RetailId == targetId && t.StatusId == 1 && t.CreatedAt >= startDate);

            var itemsQuery = _context.TransactionItems
                .Include(ti => ti.Product)
                .Where(ti => ti.Transaction.RetailId == targetId && ti.Transaction.StatusId == 1 && ti.Transaction.CreatedAt >= startDate);

            var model = new SalesAnalysisViewModel
            {
                CurrentRange = range,
                RangeLabel = rangeLabel,
                TodayTransactions = await _context.Transactions
                    .Where(t => t.RetailId == targetId && t.StatusId == 1 && t.CreatedAt.Date == today)
                    .CountAsync(),
                TotalRevenue = await transactionsQuery.Select(t => (decimal?)t.TotalAmount).SumAsync() ?? 0m,
                TotalItemsSold = await itemsQuery.Select(ti => (int?)ti.Quantity).SumAsync() ?? 0
            };

            var topProduct = await itemsQuery
                .GroupBy(ti => new { ti.ProductId, ti.Product.ProductName })
                .Select(g => new { Name = g.Key.ProductName, TotalQty = g.Sum(ti => ti.Quantity) })
                .OrderByDescending(x => x.TotalQty)
                .FirstOrDefaultAsync();

            if (topProduct != null)
            {
                model.TopSellingItemName = topProduct.Name;
            }

            // Group Revenue Graph Data
            List<string> labels = new();
            List<decimal> values = new();

            if (range == "7days" || range == "1month")
            {
                var dailyRevenue = await transactionsQuery
                    .GroupBy(t => t.CreatedAt.Date)
                    .Select(g => new { Date = g.Key, Revenue = g.Sum(t => t.TotalAmount) })
                    .ToListAsync();

                int totalDays = (today - startDate).Days;
                for (int i = 0; i <= totalDays; i++)
                {
                    var d = startDate.AddDays(i);
                    labels.Add(d.ToString("MMM dd"));
                    values.Add(dailyRevenue.FirstOrDefault(x => x.Date == d)?.Revenue ?? 0m);
                }
            }
            else
            {
                var monthlyRevenue = await transactionsQuery
                    .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
                    .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Revenue = g.Sum(t => t.TotalAmount) })
                    .ToListAsync();

                int totalMonths = range == "6months" ? 6 : 12;
                for (int i = totalMonths; i >= 0; i--)
                {
                    var m = today.AddMonths(-i);
                    labels.Add(m.ToString("MMM yyyy"));
                    values.Add(monthlyRevenue.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Revenue ?? 0m);
                }
            }

            model.RevenueDatesJson = JsonSerializer.Serialize(labels);
            model.RevenueValuesJson = JsonSerializer.Serialize(values);

            // Graph 2: Top Products
            var top5Products = await itemsQuery
                .GroupBy(ti => ti.Product.ProductName)
                .Select(g => new { Name = g.Key, Qty = g.Sum(ti => ti.Quantity) })
                .OrderByDescending(x => x.Qty)
                .Take(5)
                .ToListAsync();

            model.TopProductNamesJson = JsonSerializer.Serialize(top5Products.Select(p => p.Name));
            model.TopProductQuantitiesJson = JsonSerializer.Serialize(top5Products.Select(p => p.Qty));

            // Graph 3: Payment Methods
            var paymentMethods = await transactionsQuery
                .Include(t => t.PaymentMethod)
                .GroupBy(t => t.PaymentMethod.PaymentMethodName)
                .Select(g => new { Method = g.Key, Count = g.Count() })
                .ToListAsync();

            model.PaymentMethodLabelsJson = JsonSerializer.Serialize(paymentMethods.Select(p => p.Method));
            model.PaymentMethodCountsJson = JsonSerializer.Serialize(paymentMethods.Select(p => p.Count));

            ViewBag.RetailId = targetId;
            ViewData["RetailId"] = targetId;
            return View(model);
        }

        // GET: Retails/Transactions
        public async Task<IActionResult> TransactionLogs(int? retailId, int? id, string? search)
        {
            var targetId = retailId ?? id;
            if (targetId == null || targetId == 0)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Challenge();
            }

            // Security Check: Verify user belongs to this store as Owner or active Staff
            var store = await _context.Retails
                .Include(r => r.RetailStaffs)
                .FirstOrDefaultAsync(r => r.RetailId == targetId);

            if (store == null)
            {
                return NotFound();
            }

            bool isAuthorized = store.AdminOwner == currentUserId ||
                                store.RetailStaffs.Any(rs => rs.StaffId == currentUserId && rs.StatusId == 1);

            if (!isAuthorized)
            {
                return Forbid();
            }

            ViewData["CurrentFilter"] = search;
            ViewData["RetailId"] = targetId;
            ViewData["RetailName"] = store.RetailName;
            ViewBag.RetailId = targetId;

            var transactionsQuery = _context.Transactions
                .Include(t => t.PaymentMethod)
                .Include(t => t.Status)
                .Include(t => t.RetailStaff).ThenInclude(rs => rs.Staff)
                .Include(t => t.TransactionItems)
                .Where(t => t.RetailId == targetId)
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                transactionsQuery = transactionsQuery.Where(t =>
                    t.ReceiptNumber.Contains(search) ||
                    (t.RetailStaff != null && t.RetailStaff.Staff != null && t.RetailStaff.Staff.UserName != null && t.RetailStaff.Staff.UserName.Contains(search)) ||
                    (t.PaymentMethod != null && t.PaymentMethod.PaymentMethodName.Contains(search)));
            }

            return View(await transactionsQuery.ToListAsync());
        }

        // POST: Retails/ExportExcel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportExcel(int retailId, string? revenueChartImg, string? productsChartImg, string? paymentChartImg, string range = "7days")
        {
            DateTime startDate = range switch
            {
                "1month" => DateTime.Today.AddMonths(-1),
                "6months" => DateTime.Today.AddMonths(-6),
                "1year" => DateTime.Today.AddYears(-1),
                _ => DateTime.Today.AddDays(-6)
            };

            var transactions = await _context.Transactions
                .Include(t => t.PaymentMethod)
                .Include(t => t.Status)
                .Include(t => t.RetailStaff).ThenInclude(rs => rs.Staff)
                .Include(t => t.TransactionItems).ThenInclude(ti => ti.Product)
                .Where(t => t.RetailId == retailId && t.CreatedAt >= startDate)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var store = await _context.Retails.FindAsync(retailId);
            string safeStoreName = string.IsNullOrWhiteSpace(store?.RetailName) ? $"Store_{retailId}" : store.RetailName.Replace(" ", "_");

            var sevenDaysAgo = DateTime.Today.AddDays(-6);
            int txLast7Days = transactions.Count(t => t.CreatedAt >= sevenDaysAgo);
            int totalSoldItems = transactions.SelectMany(t => t.TransactionItems).Sum(ti => ti.Quantity);

            var productSales = transactions.SelectMany(t => t.TransactionItems)
                .GroupBy(ti => ti.Product?.ProductName ?? "Unknown")
                .Select(g => new { Name = g.Key, Qty = g.Sum(ti => ti.Quantity) })
                .OrderByDescending(x => x.Qty)
                .ToList();

            string topProduct = productSales.FirstOrDefault()?.Name ?? "N/A";
            string leastProduct = productSales.LastOrDefault()?.Name ?? "N/A";

            using var workbook = new XLWorkbook();

            // SHEET 1: TRANSACTIONS
            var ws1 = workbook.Worksheets.Add("Transactions");
            ws1.Cell(1, 1).Value = $"{store?.RetailName ?? "Store"} Sales Report ({range})";
            ws1.Cell(1, 1).Style.Font.Bold = true;
            ws1.Cell(1, 1).Style.Font.FontSize = 14;
            ws1.Cell(2, 1).Value = $"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            ws1.Cell(4, 1).Value = "--- STORE PERFORMANCE SUMMARY ---";
            ws1.Cell(4, 1).Style.Font.Bold = true;
            ws1.Cell(4, 1).Style.Font.FontColor = XLColor.FromHtml("#1162A3");

            ws1.Cell(5, 1).Value = "Transactions (Last 7 Days):";
            ws1.Cell(5, 2).Value = txLast7Days;
            ws1.Cell(6, 1).Value = "Total Items Sold:";
            ws1.Cell(6, 2).Value = totalSoldItems;
            ws1.Cell(7, 1).Value = "Top Selling Product:";
            ws1.Cell(7, 2).Value = topProduct;
            ws1.Cell(8, 1).Value = "Least Selling Product:";
            ws1.Cell(8, 2).Value = leastProduct;

            int tableStartRow = 11;
            string[] h1 = { "Receipt #", "Date", "Cashier", "Payment Method", "Items Count", "Status", "Total Amount" };
            for (int i = 0; i < h1.Length; i++)
            {
                var cell = ws1.Cell(tableStartRow, i + 1);
                cell.Value = h1[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1162A3");
                cell.Style.Font.FontColor = XLColor.White;
            }

            int row1 = tableStartRow + 1;
            foreach (var t in transactions)
            {
                ws1.Cell(row1, 1).Value = t.ReceiptNumber;
                ws1.Cell(row1, 2).Value = t.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                ws1.Cell(row1, 3).Value = t.RetailStaff?.Staff?.UserName ?? "N/A";
                ws1.Cell(row1, 4).Value = t.PaymentMethod?.PaymentMethodName ?? "N/A";
                ws1.Cell(row1, 5).Value = t.TransactionItems.Sum(ti => ti.Quantity);
                ws1.Cell(row1, 6).Value = t.Status?.StatusName ?? "Completed";
                ws1.Cell(row1, 7).Value = t.TotalAmount;
                ws1.Cell(row1, 7).Style.NumberFormat.Format = "₱#,##0.00";
                row1++;
            }
            ws1.Columns().AdjustToContents();

            // SHEET 2: TRANSACTION ITEMS
            var ws2 = workbook.Worksheets.Add("Transaction Items");
            ws2.Cell(1, 1).Value = $"{store?.RetailName ?? "Store"} Item Details";
            ws2.Cell(1, 1).Style.Font.Bold = true;
            ws2.Cell(1, 1).Style.Font.FontSize = 14;

            string[] h2 = { "Receipt #", "Date", "Product Name", "Unit Price", "Quantity", "Subtotal" };
            for (int i = 0; i < h2.Length; i++)
            {
                var cell = ws2.Cell(3, i + 1);
                cell.Value = h2[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#198754");
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

            // SHEET 3: CHARTS
            var ws3 = workbook.Worksheets.Add("Visualizations");
            ws3.Cell(1, 1).Value = "Sales Visualizations";
            ws3.Cell(1, 1).Style.Font.Bold = true;
            ws3.Cell(1, 1).Style.Font.FontSize = 14;

            int chartRow = 3;
            void AddChartToExcel(string? base64, string title)
            {
                var bytes = SafeBase64ToBytes(base64);
                if (bytes != null && bytes.Length > 0)
                {
                    ws3.Cell(chartRow, 1).Value = title;
                    ws3.Cell(chartRow, 1).Style.Font.Bold = true;

                    using var ms = new MemoryStream(bytes);
                    var pic = ws3.AddPicture(ms).MoveTo(ws3.Cell(chartRow + 1, 1));
                    pic.Scale(0.65);
                    chartRow += 20;
                }
            }

            AddChartToExcel(revenueChartImg, $"Revenue Trend ({range})");
            AddChartToExcel(productsChartImg, "Top Selling Products");
            AddChartToExcel(paymentChartImg, "Payment Method Distribution");

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"SalesReport_{safeStoreName}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // POST: Retails/ExportPdf
        // POST: Retails/ExportPdf
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportPdf(int retailId, string? revenueChartImg, string? productsChartImg, string? paymentChartImg, [FromServices] IWebHostEnvironment env, string range = "7days")
        {
            DateTime startDate = range switch
            {
                "1month" => DateTime.Today.AddMonths(-1),
                "6months" => DateTime.Today.AddMonths(-6),
                "1year" => DateTime.Today.AddYears(-1),
                _ => DateTime.Today.AddDays(-6)
            };

            var transactions = await _context.Transactions
                .Include(t => t.PaymentMethod)
                .Include(t => t.Status)
                .Include(t => t.RetailStaff).ThenInclude(rs => rs.Staff)
                .Include(t => t.TransactionItems).ThenInclude(ti => ti.Product)
                .Where(t => t.RetailId == retailId && t.CreatedAt >= startDate)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var store = await _context.Retails.FindAsync(retailId);
            string safeStoreName = string.IsNullOrWhiteSpace(store?.RetailName) ? $"Store_{retailId}" : store.RetailName.Replace(" ", "");

            var totalRevenue = transactions.Sum(t => t.TotalAmount);
            var sevenDaysAgo = DateTime.Today.AddDays(-6);
            int txLast7Days = transactions.Count(t => t.CreatedAt >= sevenDaysAgo);
            int totalSoldItems = transactions.SelectMany(t => t.TransactionItems).Sum(ti => ti.Quantity);

            var productSales = transactions.SelectMany(t => t.TransactionItems)
                .GroupBy(ti => ti.Product?.ProductName ?? "Unknown")
                .Select(g => new { Name = g.Key, Qty = g.Sum(ti => ti.Quantity) })
                .OrderByDescending(x => x.Qty)
                .ToList();

            string topProduct = productSales.FirstOrDefault()?.Name ?? "N/A";
            string leastProduct = productSales.LastOrDefault()?.Name ?? "N/A";

            var logoPath = Path.Combine(env.WebRootPath, "images", "SmartScanLogo.png");
            byte[] logoBytes = System.IO.File.Exists(logoPath) ? await System.IO.File.ReadAllBytesAsync(logoPath) : Array.Empty<byte>();

            byte[]? revChartBytes = SafeBase64ToBytes(revenueChartImg);
            byte[]? prodChartBytes = SafeBase64ToBytes(productsChartImg);
            byte[]? payChartBytes = SafeBase64ToBytes(paymentChartImg);

            var pdfDocument = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(35);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Lato));

                    // Content Area (Page 1 gets branding, Subsequent pages don't)
                    page.Content().Column(col =>
                    {
                        // ================= PAGE 1 ONLY: BRANDING HEADER =================
                        col.Item().Row(row =>
                        {
                            if (logoBytes.Length > 0)
                            {
                                row.ConstantItem(55).Height(55).Image(logoBytes).FitArea();
                            }

                            row.RelativeItem().PaddingLeft(10).Column(c =>
                            {
                                c.Item().Text("SMARTSCAN: A Vision-Based Automated Checkout For Retail Counters").Bold().FontSize(10).FontColor("#1162A3");
                                c.Item().PaddingTop(3).Text($"Retail Partner: {store?.RetailName ?? "N/A"}").Bold().FontSize(15).FontColor(Colors.Black);
                                c.Item().Text($"Report Generated: {DateTime.Now:MMMM dd, yyyy - hh:mm tt}").FontSize(8.5f).FontColor(Colors.Grey.Medium);
                            });

                            row.ConstantItem(125).AlignRight().Column(c =>
                            {
                                c.Item().Text("TOTAL REVENUE").FontSize(8).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"₱{totalRevenue:N2}").Bold().FontSize(16).FontColor("#198754");
                            });
                        });

                        col.Item().PaddingTop(8).PaddingBottom(10).LineHorizontal(1.5f).LineColor(Colors.Grey.Lighten2);

                        // ================= PERFORMANCE SUMMARY TABLE =================
                        col.Item().PaddingBottom(5).Text("Store Performance Summary").Bold().FontSize(12).FontColor("#1162A3");
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Table(table =>
                            {
                                table.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.RelativeColumn(); });
                                table.Cell().PaddingBottom(2).Text("Transactions (Last 7 Days):").SemiBold();
                                table.Cell().PaddingBottom(2).Text(txLast7Days.ToString());
                                table.Cell().PaddingBottom(2).Text("Total Items Sold:").SemiBold();
                                table.Cell().PaddingBottom(2).Text(totalSoldItems.ToString());
                                table.Cell().PaddingBottom(2).Text("Top Selling Product:").SemiBold();
                                table.Cell().PaddingBottom(2).Text(topProduct).FontColor("#198754");
                                table.Cell().PaddingBottom(2).Text("Least Selling Product:").SemiBold();
                                table.Cell().PaddingBottom(2).Text(leastProduct).FontColor("#dc3545");
                            });
                        });

                        // ================= SHORTER & JUSTIFIED NARRATIVE =================
                        col.Item().PaddingVertical(8).Text(text =>
                        {
                            text.Justify();
                            text.Span("Transaction volumes demonstrate stable retail velocity, anchored by consistent demand for ").FontColor(Colors.Grey.Darken3);
                            text.Span($"{topProduct}").SemiBold();
                            text.Span(". Targeted promotions for lower-turnover items like ").FontColor(Colors.Grey.Darken3);
                            text.Span($"{leastProduct}").SemiBold();
                            text.Span(" will optimize shelf turnover, while SmartScan automated checkout guarantees efficient customer throughput.").FontColor(Colors.Grey.Darken3);
                        });

                        col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten3);

                        // ================= GRAPHS BLOCK =================
                        if (revChartBytes != null || prodChartBytes != null || payChartBytes != null)
                        {
                            col.Item().ShowEntire().Column(graphCol =>
                            {
                                graphCol.Item().PaddingBottom(8).Text("Sales Visualizations").Bold().FontSize(12).FontColor("#1162A3");

                                if (revChartBytes != null || payChartBytes != null)
                                {
                                    graphCol.Item().Row(row =>
                                    {
                                        if (revChartBytes != null)
                                        {
                                            row.RelativeItem(2).PaddingRight(10).Column(c =>
                                            {
                                                c.Item().AlignCenter().PaddingBottom(4).Text("Revenue Trend (Last 7 Days)").FontSize(9.5f).SemiBold().FontColor(Colors.Grey.Darken2);
                                                c.Item().MaxHeight(155).Image(revChartBytes).FitArea();
                                            });
                                        }

                                        if (payChartBytes != null)
                                        {
                                            row.RelativeItem(1).Column(c =>
                                            {
                                                c.Item().AlignCenter().PaddingBottom(4).Text("Payment Method Distribution").FontSize(9.5f).SemiBold().FontColor(Colors.Grey.Darken2);
                                                c.Item().MaxHeight(155).Image(payChartBytes).FitArea();
                                            });
                                        }
                                    });
                                }

                                if (prodChartBytes != null)
                                {
                                    graphCol.Item().PaddingTop(10).Column(c =>
                                    {
                                        c.Item().AlignCenter().PaddingBottom(4).Text("Top Selling Products by Volume").FontSize(9.5f).SemiBold().FontColor(Colors.Grey.Darken2);
                                        c.Item().MaxHeight(145).Image(prodChartBytes).FitArea();
                                    });
                                }
                            });
                        }

                        // ================= PAGE 2: RECENT TRANSACTIONS =================
                        col.Item().PageBreak();

                        col.Item().PaddingBottom(6).Text("Recent Transactions").Bold().FontSize(13).FontColor("#1162A3");
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(2);
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

                            foreach (var t in transactions.Take(50))
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(t.ReceiptNumber);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(t.CreatedAt.ToString("MM/dd/yyyy"));
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(t.RetailStaff?.Staff?.UserName ?? "N/A");
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(t.PaymentMethod?.PaymentMethodName ?? "N/A");
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(t.TransactionItems.Sum(x => x.Quantity).ToString());
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"₱{t.TotalAmount:N2}");
                            }
                        });

                        // ================= PAGE 3: TRANSACTION ITEM DETAILS =================
                        col.Item().PageBreak();

                        col.Item().PaddingBottom(6).Text("Transaction Item Details").Bold().FontSize(13).FontColor("#198754");
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);    // Receipt
                                cols.RelativeColumn(3);    // Product
                                cols.RelativeColumn(1.5f); // Unit Price
                                cols.RelativeColumn(1);    // Qty
                                cols.RelativeColumn(2);    // Subtotal
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background("#198754").Padding(5).Text("Receipt #").FontColor(Colors.White).Bold();
                                h.Cell().Background("#198754").Padding(5).Text("Product Name").FontColor(Colors.White).Bold();
                                h.Cell().Background("#198754").Padding(5).AlignRight().Text("Unit Price").FontColor(Colors.White).Bold();
                                h.Cell().Background("#198754").Padding(5).AlignRight().Text("Qty").FontColor(Colors.White).Bold();
                                h.Cell().Background("#198754").Padding(5).AlignRight().Text("Subtotal").FontColor(Colors.White).Bold();
                            });

                            foreach (var t in transactions.Take(50))
                            {
                                foreach (var item in t.TransactionItems)
                                {
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(t.ReceiptNumber).FontColor(Colors.Grey.Darken1);
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(item.Product?.ProductName ?? "Unknown");
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"₱{item.UnitPrice:N2}");
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(item.Quantity.ToString());
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"₱{(item.UnitPrice * item.Quantity):N2}");
                                }
                            }
                        });
                    });

                    // Global Page Number Footer (All pages)
                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" of ");
                        t.TotalPages();
                    });
                });
            });

            return File(pdfDocument.GeneratePdf(), "application/pdf", $"SalesReport_{safeStoreName}_{DateTime.Now:yyyyMMdd}.pdf");
        }

        private static byte[]? SafeBase64ToBytes(string? base64String)
        {
            if (string.IsNullOrWhiteSpace(base64String))
                return null;

            try
            {
                var clean = base64String;
                if (clean.Contains(','))
                {
                    clean = clean[(clean.IndexOf(',') + 1)..];
                }
                return Convert.FromBase64String(clean.Trim());
            }
            catch
            {
                return null;
            }
        }

        private bool RetailExists(int? retailid)
        {
            return _context.Retails.Any(e => e.RetailId == retailid);
        }
    }
}