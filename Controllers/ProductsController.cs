using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;

[Authorize(Roles = "Admin")]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ProductsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
    }

    // GET: PRODUCTS
    public async Task<IActionResult> Index(string search)
    {
        ViewData["CurrentFilter"] = search;

        var productsQuery = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Status)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            bool isNumeric = decimal.TryParse(search, out decimal parsedPrice);

            productsQuery = productsQuery.Where(p =>
                p.ProductName.Contains(search) ||
               (p.Category != null && p.Category.CategoryName.Contains(search)) ||
               (isNumeric && p.Price == parsedPrice));
        }

        var products = await productsQuery.ToListAsync();

        return View(products);
    }

    // GET: PRODUCTS/Create
    public IActionResult Create()
    {
        ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName");
        ViewData["StatusId"] = new SelectList(_context.Statuses, "StatusId", "StatusName");
        return View();
    }

    // POST: PRODUCTS/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ProductName,CategoryId,Price,ProductImage,ImageUpload,Barcode")] Product product)
    {
        ModelState.Remove("Category");
        ModelState.Remove("Status");

        // Strip implicit [Required] validation error on ProductImage if an image file was uploaded
        if (product.ImageUpload != null && product.ImageUpload.Length > 0)
        {
            ModelState.Remove("ProductImage");
        }
        else if (string.IsNullOrWhiteSpace(product.ProductImage))
        {
            ModelState.AddModelError("ProductImage", "Please provide an image URL or upload an image file.");
        }

        // Checks in the database for similar product name before adding
        if (!string.IsNullOrWhiteSpace(product.ProductName))
        {
            string exactName = product.ProductName.Trim().ToLower();
            string variation1 = exactName.EndsWith("s") ? exactName.Substring(0, exactName.Length - 1) : exactName + "s";
            string variation2 = exactName.EndsWith("es") ? exactName.Substring(0, exactName.Length - 2) : exactName + "es";

            bool nameExists = await _context.Products
                .AnyAsync(p =>
                    p.ProductName.Trim().ToLower() == exactName ||
                    p.ProductName.Trim().ToLower() == variation1 ||
                    p.ProductName.Trim().ToLower() == variation2);

            if (nameExists)
            {
                ModelState.AddModelError("ProductName", $"A product similar to '{product.ProductName.Trim()}' already exists.");
            }
        }

        // Check if barcode already exists
        if (!string.IsNullOrWhiteSpace(product.Barcode))
        {
            product.Barcode = product.Barcode.Trim();
            bool barcodeExists = await _context.Products.AnyAsync(p => p.Barcode == product.Barcode);
            if (barcodeExists)
            {
                ModelState.AddModelError("Barcode", $"The barcode '{product.Barcode}' is already assigned to another product.");
            }
        }

        if (ModelState.IsValid)
        {
            // Save uploaded file to wwwroot/uploads/products
            if (product.ImageUpload != null && product.ImageUpload.Length > 0)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products");
                Directory.CreateDirectory(uploadsFolder);

                string cleanFileName = Path.GetFileName(product.ImageUpload.FileName);
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + cleanFileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await product.ImageUpload.CopyToAsync(fileStream);
                }

                product.ProductImage = "/uploads/products/" + uniqueFileName;
            }

            product.StatusId = 1;
            product.CanScan = false;
            product.IsAgeRestricted = false;

            _context.Add(product);
            await _context.SaveChangesAsync();

            TempData["ProductMessage"] = $"{product.ProductName} has been created successfully.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName", product.CategoryId);
        return View(product);
    }

    // GET: PRODUCTS/Edit/5
    public async Task<IActionResult> Edit(int? productid)
    {
        if (productid == null)
        {
            return NotFound();
        }

        var product = await _context.Products.FindAsync(productid);
        if (product == null)
        {
            return NotFound();
        }

        ViewBag.ProductImage = product.ProductImage;

        ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName");
        ViewData["StatusId"] = new SelectList(_context.Statuses, "StatusId", "StatusName");
        return View(product);
    }

    // POST: PRODUCTS/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? productid, [Bind("ProductId,ProductName,CategoryId,Price,ProductImage,StatusId,ImageUpload,IsAgeRestricted,Barcode")] Product product)
    {
        if (productid != product.ProductId)
        {
            return NotFound();
        }

        ModelState.Remove("Category");
        ModelState.Remove("Status");

        var existProduct = await _context.Products.FindAsync(productid);
        if (existProduct == null)
        {
            return NotFound();
        }

        // Capture the previous image path before modifying anything
        string? oldImagePath = existProduct.ProductImage;

        // Check if barcode already exists on a DIFFERENT product
        if (!string.IsNullOrWhiteSpace(product.Barcode))
        {
            product.Barcode = product.Barcode.Trim();
            bool barcodeExists = await _context.Products
                .AnyAsync(p => p.Barcode == product.Barcode && p.ProductId != productid);

            if (barcodeExists)
            {
                ModelState.AddModelError("Barcode", $"The barcode '{product.Barcode}' is already assigned to another product.");
            }
        }

        // Process new file upload if supplied
        if (product.ImageUpload != null && product.ImageUpload.Length > 0)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(uploadsFolder);

            string cleanFileName = Path.GetFileName(product.ImageUpload.FileName);
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + cleanFileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await product.ImageUpload.CopyToAsync(fileStream);
            }

            product.ProductImage = "/uploads/products/" + uniqueFileName;
            ModelState.Remove("ProductImage");
        }
        else if (string.IsNullOrWhiteSpace(product.ProductImage))
        {
            // Retain existing image if neither a new file nor a new URL was provided
            product.ProductImage = existProduct.ProductImage;
            ModelState.Remove("ProductImage");
        }

        if (string.IsNullOrWhiteSpace(product.ProductImage))
        {
            ModelState.AddModelError("ProductImage", "Please provide a valid image URL or upload an image file.");
        }

        if (ModelState.IsValid)
        {
            existProduct.ProductName = product.ProductName?.Trim();
            existProduct.CategoryId = product.CategoryId;
            existProduct.Price = product.Price;
            existProduct.ProductImage = product.ProductImage;
            existProduct.StatusId = product.StatusId;
            existProduct.IsAgeRestricted = product.IsAgeRestricted;
            existProduct.Barcode = product.Barcode;

            try
            {
                await _context.SaveChangesAsync();

                // Delete old uploaded file if it originated from /uploads/products/ and was replaced
                if (!string.IsNullOrWhiteSpace(oldImagePath) &&
                    !string.Equals(oldImagePath, existProduct.ProductImage, StringComparison.OrdinalIgnoreCase) &&
                    oldImagePath.StartsWith("/uploads/products/", StringComparison.OrdinalIgnoreCase))
                {
                    DeleteLocalUpload(oldImagePath);
                }

                TempData["ProductMessage"] = $"{existProduct.ProductName} was updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(product.ProductId))
                {
                    return NotFound();
                }
                throw;
            }
        }

        ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName", product.CategoryId);
        ViewData["StatusId"] = new SelectList(_context.Statuses, "StatusId", "StatusName", product.StatusId);
        return View(product);
    }

    // GET: PRODUCTS/Delete/5
    public async Task<IActionResult> Delete(int? productid)
    {
        if (productid == null)
        {
            return NotFound();
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(m => m.ProductId == productid);
        if (product == null)
        {
            return NotFound();
        }

        return View(product);
    }

    // POST: PRODUCTS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? productid)
    {
        var product = await _context.Products.FindAsync(productid);

        if (product == null)
        {
            return NotFound();
        }

        string? imagePath = product.ProductImage;

        try
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            // Clean up uploaded image file when a product is deleted
            if (!string.IsNullOrWhiteSpace(imagePath) &&
                imagePath.StartsWith("/uploads/products/", StringComparison.OrdinalIgnoreCase))
            {
                DeleteLocalUpload(imagePath);
            }
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Cannot delete this product because inventories are assigned to it.";
        }

        return RedirectToAction(nameof(Index));
    }

    private bool ProductExists(int? productid)
    {
        return _context.Products.Any(e => e.ProductId == productid);
    }

    private void DeleteLocalUpload(string relativeUrlPath)
    {
        string relativeFilePath = relativeUrlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string fullPhysicalPath = Path.Combine(_webHostEnvironment.WebRootPath, relativeFilePath);

        if (System.IO.File.Exists(fullPhysicalPath))
        {
            System.IO.File.Delete(fullPhysicalPath);
        }
    }
}