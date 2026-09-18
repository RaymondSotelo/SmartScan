
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;

[Authorize(Roles = "Admin")]
public class InventoriesController : Controller
{
    private readonly ApplicationDbContext _context;

    public InventoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: INVENTORYS
    public async Task<IActionResult> Index(int? retailId, string search)    
    {
        ViewData["CurrentFilter"] = search;

        if (retailId == null)
        {
            return NotFound();
        }

        //Fetch store name
        var storeName = await _context.Retails
            .Where(r => r.RetailId == retailId)
            .Select(r => r.RetailName)
            .FirstOrDefaultAsync();

        //Fetch Inv data
        var InvQuery = _context.Inventories
            .Include(i => i.Product)
                .ThenInclude(p => p.Category)
            .Include(i => i.Retail)
            .Where(i => i.RetailId == retailId)
            .AsQueryable();

        //Store retailId
        ViewBag.RetailId = retailId;

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            bool isPrice = decimal.TryParse(search, out decimal parsedPrice);
            bool isNumeric = int.TryParse(search, out int parsedNumeric);

            InvQuery = InvQuery.Where(i =>
               (i.Product != null && i.Product.ProductName.Contains(search)) ||
               (isPrice && i.LocalPrice == parsedPrice) ||
               (isNumeric && i.Stock == parsedNumeric));
        }

        ViewBag.RetailName = storeName;
        ViewData["ProductId"] = new SelectList(_context.Products, "ProductId", "ProductName");

        return View(await InvQuery.ToListAsync());
    }

    // GET: INVENTORYS/Create
    public async Task<IActionResult> Create(int? retailId)
    {
        if (retailId == null)
        {
            return NotFound();
        }

        ViewBag.RetailId = retailId;
        ViewData["ProductId"] = new SelectList(_context.Products, "ProductId", "ProductName");

        ViewBag.ProductList = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.StatusId == 1)
            .ToListAsync();

        return View();
    }

    // POST: INVENTORYS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ProductId,LocalPrice,Stock,RetailId")] Inventory inventory)
    {
        ModelState.Remove("Product");
        ModelState.Remove("Retail");

        // Checks if the product is already exist in the inventory
        bool productExist = await _context.Inventories
            .AnyAsync(i => i.RetailId == inventory.RetailId && i.ProductId == inventory.ProductId);

        if (productExist)
        {
            // Fetch for product name for cleaner error message
            var product = await _context.Products.FindAsync(inventory.ProductId);
            string productName = product?.ProductName ?? "This product";

            ModelState.AddModelError("ProductId", $"{productName} is already in your inventory.");
        }

        if (ModelState.IsValid)
        {
            _context.Add(inventory);
            await _context.SaveChangesAsync();

            //Redirect back of the same retailId
            return RedirectToAction(nameof(Index), new { retailId = inventory.RetailId});
        }

        ViewBag.RetailId = inventory.RetailId;
        ViewBag.ProductList = await _context.Products
            .Include(p => p.Category)
            .ToListAsync();

        ViewData["ProductId"] = new SelectList(_context.Products, "ProductId", "ProductName", inventory.ProductId);
        return View(inventory);
    }

    // GET: INVENTORYS/Edit/5
    public async Task<IActionResult> Edit(int? inventoryid, int? retailId, string? barcodeSearch)
    {
        if (inventoryid == null)
        {
            return NotFound();
        }

        var inventory = await _context.Inventories
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.InventoryId == inventoryid);

        if (inventory == null)
        {
            return NotFound();
        }

        var barcodeQuery = _context.Barcodes
            .Where(b => b.InventoryId == inventoryid);

        if (!string.IsNullOrWhiteSpace(barcodeSearch))
        {
            string term = barcodeSearch.Trim();
            barcodeQuery = barcodeQuery.Where(b => b.BarcodeLine.Contains(term));
        }

        ViewBag.BarcodeList = await barcodeQuery.ToListAsync();
        ViewBag.BarcodeSearch = barcodeSearch;
        ViewBag.RetailId = retailId ?? inventory.RetailId;
        return View(inventory);
    }

    // POST: INVENTORYS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? inventoryid, [Bind("InventoryId,ProductId,LocalPrice,Stock,BarcodeId,RetailId")] Inventory inventory)
    {
        if (inventoryid != inventory.InventoryId)
        {
            return NotFound();
        }

        var updInv = await _context.Inventories
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.InventoryId == inventoryid);

        if (updInv == null)
        {
            return NotFound();
        }

        ModelState.Remove("Product");
        ModelState.Remove("Retail");

        if (ModelState.IsValid)
        {
            try
            {
                updInv.LocalPrice = inventory.LocalPrice;
                updInv.Stock = inventory.Stock;

                await _context.SaveChangesAsync();
                
                return RedirectToAction(nameof(Index), new { retailId = inventory.RetailId });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InventoryExists(inventory.InventoryId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        ViewBag.RetailId = inventory.RetailId;
        ViewBag.ProductList = await _context.Products
            .Include(p => p.Category)
            .ToListAsync();

        ViewData["ProductId"] = new SelectList(_context.Products, "ProductId", "ProductName", inventory.ProductId);
        return View(inventory);
    }

    // GET: INVENTORYS/Delete/5
    public async Task<IActionResult> Delete(int? inventoryid)
    {
        if (inventoryid == null)
        {
            return NotFound();
        }

        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(m => m.InventoryId == inventoryid);
        if (inventory == null)
        {
            return NotFound();
        }

        return View(inventory);
    }

    // POST: INVENTORYS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? inventoryid)
    {
        if (inventoryid == null)
        {
            return NotFound();
        }

        var inventory = await _context.Inventories.FindAsync(inventoryid);

        if (inventory == null)
        {
            return NotFound();
        }

        int retailId = inventory.RetailId;

        _context.Inventories.Remove(inventory);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { retailId = retailId });
    }

    private bool InventoryExists(int? inventoryid)
    {
        return _context.Inventories.Any(e => e.InventoryId == inventoryid);
    }

    //POST: Add Barcode
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBarcode(int? retailId, int? inventoryid, [Bind("BarcodeLine")] Barcode barcode)
    {
        var inventory = await _context.Inventories
        .Include(i => i.Product)
            .ThenInclude(p => p.Category)
        .FirstOrDefaultAsync(i => i.InventoryId == inventoryid);

        if (inventory == null)
        {
            return NotFound();
        }

        barcode.InventoryId = inventory.InventoryId;

        ModelState.Remove("Inventory");

        // Ensure if barcode is not just white line
        if (string.IsNullOrWhiteSpace(barcode.BarcodeLine))
        {
            ModelState.AddModelError("Barcode", "Barcode cannot be empty.");
        }
        else
        {
            barcode.BarcodeLine = barcode.BarcodeLine.Trim();
            if (barcode.BarcodeLine.Length > 13)
            {
                ModelState.AddModelError("BarcodeLine", "Barcode cannot exceed 13 characters.");
            }

            bool barcodeExists = await _context.Barcodes
                .AnyAsync(b => b.BarcodeLine == barcode.BarcodeLine.Trim());
            if (barcodeExists)
            {
                ModelState.AddModelError("Barcode", $"Barcode '{barcode.BarcodeLine}' is already registered.");
            }
        }

        if (ModelState.IsValid)
        {
            _context.Barcodes.Add(barcode);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Barcode successfully added!";
            return RedirectToAction(nameof(Edit), new { inventoryid = barcode.InventoryId, retailId = retailId });
        }

        ViewBag.BarcodeList = await _context.Barcodes
        .Where(b => b.InventoryId == barcode.InventoryId)
        .ToListAsync();

        ViewBag.RetailId = retailId ?? inventory.RetailId;

        return View("Edit", inventory);
    }

    //POST: Delete Barcode
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBarcode(int barcodeId, int inventoryId, int? retailId, string? barcodeSearch)
    {
        var barcode = await _context.Barcodes.FindAsync(barcodeId);

        if (barcode != null)
        {
            _context.Barcodes.Remove(barcode);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Edit), new { inventoryid = inventoryId, retailId = retailId, barcodeSearch = barcodeSearch });
    }
}
