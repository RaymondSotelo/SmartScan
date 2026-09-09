
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartScan.Models;
using SmartScan.Data;

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
                i.Barcode.Contains(search) ||
               (i.Product != null && i.Product.ProductName.Contains(search)) ||
               (isPrice && i.LocalPrice == parsedPrice) ||
               (isNumeric && i.Stock == parsedNumeric));
        }

        ViewBag.RetailName = storeName;

        return View(await InvQuery.ToListAsync());
    }

    // GET: INVENTORYS/Details/5
    public async Task<IActionResult> Details(int? inventoryid)
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

    // GET: INVENTORYS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: INVENTORYS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("InventoryId,ProductId,LocalPrice,Stock,Barcode,RetailId,Product,Retail")] Inventory inventory)
    {
        if (ModelState.IsValid)
        {
            _context.Add(inventory);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(inventory);
    }

    // GET: INVENTORYS/Edit/5
    public async Task<IActionResult> Edit(int? inventoryid)
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
        return View(inventory);
    }

    // POST: INVENTORYS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? inventoryid, [Bind("InventoryId,ProductId,LocalPrice,Stock,Barcode,RetailId,Product,Retail")] Inventory inventory)
    {
        if (inventoryid != inventory.InventoryId)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(inventory);
                await _context.SaveChangesAsync();
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
            return RedirectToAction(nameof(Index));
        }
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
        var inventory = await _context.Inventories.FindAsync(inventoryid);
        if (inventory != null)
        {
            _context.Inventories.Remove(inventory);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool InventoryExists(int? inventoryid)
    {
        return _context.Inventories.Any(e => e.InventoryId == inventoryid);
    }
}
