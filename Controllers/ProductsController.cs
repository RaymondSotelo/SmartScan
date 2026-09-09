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

    public ProductsController(ApplicationDbContext context)
    {
        _context = context;
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
    public async Task<IActionResult> Create([Bind("ProductName,CategoryId,Price,ProductImage")] Product product)
    {
        ModelState.Remove("Status");
        ModelState.Remove("Category");
        ModelState.Remove("StatusId");

        if (ModelState.IsValid)
        {
            product.StatusId = 1;

            _context.Add(product);
            await _context.SaveChangesAsync();
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
    public async Task<IActionResult> Edit(int? productid, [Bind("ProductId,ProductName,CategoryId,Price,ProductImage,StatusId")] Product product)
    {
        if (productid != product.ProductId)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var existProduct = await _context.Products.FindAsync(productid);
            if (existProduct == null)
            {
                return NotFound();
            }

            existProduct.ProductName = product.ProductName;
            existProduct.CategoryId = product.CategoryId;
            existProduct.Price = product.Price;
            existProduct.ProductImage = product.ProductImage;
            existProduct.StatusId = product.StatusId;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(product.ProductId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }

        ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName", product.CategoryId);
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
            TempData["SuccessMessage"] = "Product deleted successfully.";
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
