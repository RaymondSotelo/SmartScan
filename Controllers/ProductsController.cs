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
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ProductName,CategoryId,Price,ProductImage,ImageUpload")] Product product)
    {
        // Ignore relational navigation properties during model validation
        ModelState.Remove("Category");
        ModelState.Remove("Status");

        // Checks if an image was uploaded via file tab
        if (product.ImageUpload != null && product.ImageUpload.Length > 0)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
            Directory.CreateDirectory(uploadsFolder);

            // Sanitize file name
            string cleanFileName = Path.GetFileName(product.ImageUpload.FileName);
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + cleanFileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await product.ImageUpload.CopyToAsync(fileStream);
            }

            // Assign the saved file path to the non-nullable string
            product.ProductImage = "/uploads/products/" + uniqueFileName;

            // Strip the model validation error that was generated on initial binding
            ModelState.Remove("ProductImage");
        }

        // Fallback check: ensure either a URL was provided or a file was saved
        if (string.IsNullOrWhiteSpace(product.ProductImage))
        {
            ModelState.AddModelError("ProductImage", "Please provide a valid image URL or upload an image file.");
        }

        // Save to database
        if (ModelState.IsValid)
        {
            product.StatusId = 1;

            _context.Add(product);
            await _context.SaveChangesAsync();
            TempData["UserMessage"] = $"{product.ProductName} was successfully added.";
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
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? productid, [Bind("ProductId,ProductName,CategoryId,Price,ProductImage,StatusId,ImageUpload")] Product product)
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

        // Process new file upload if supplied
        if (product.ImageUpload != null && product.ImageUpload.Length > 0)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
            Directory.CreateDirectory(uploadsFolder);

            string cleanFileName = Path.GetFileName(product.ImageUpload.FileName);
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + cleanFileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await product.ImageUpload.CopyToAsync(fileStream);
            }

            // Assign newly generated local file path
            product.ProductImage = "/uploads/products/" + uniqueFileName;
            ModelState.Remove("ProductImage");
        }
        else if (string.IsNullOrWhiteSpace(product.ProductImage))
        {
            // Retain the existing image if the user didn't enter a new URL or upload a file
            product.ProductImage = existProduct.ProductImage;
            ModelState.Remove("ProductImage");
        }

        // Verify that an image path exists
        if (string.IsNullOrWhiteSpace(product.ProductImage))
        {
            ModelState.AddModelError("ProductImage", "Please provide a valid image URL or upload an image file.");
        }

        if (ModelState.IsValid)
        {
            // 5. Update existing entity fields
            existProduct.ProductName = product.ProductName?.Trim();
            existProduct.CategoryId = product.CategoryId;
            existProduct.Price = product.Price;
            existProduct.ProductImage = product.ProductImage;
            existProduct.StatusId = product.StatusId;

            try
            {
                await _context.SaveChangesAsync();
                TempData["UserMessage"] = $"{existProduct.ProductName} was updated successfully.";
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

        // Reload dropdown options if validation fails
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

        try
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
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
}
