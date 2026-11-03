
using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartScan.Data;
using SmartScan.Models;

[Authorize(Roles = "Admin")]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: CATEGORYS
    public async Task<IActionResult> Index(string search)
    {
        ViewData["CurrentFilter"] = search;

        var category = _context.Categories
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            category = category.Where(c =>
                c.CategoryName.Contains(search));
        }

        return View(await category.ToListAsync());
    }

    // POST: CATEGORIES/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("CategoryName")] Category category)
    {
        ModelState.Remove("Products");

        if (string.IsNullOrWhiteSpace(category.CategoryName))
        {
            TempData["CategoryMessage"] = "Category name cannot be empty.";
            return RedirectToAction(nameof(Index));
        }

        category.CategoryName = category.CategoryName.Trim();
        string exactName = category.CategoryName.ToLower();

        string variation1 = exactName.EndsWith("s") ? exactName.Substring(0, exactName.Length - 1) : exactName + "s";
        string variation2 = exactName.EndsWith("es") ? exactName.Substring(0, exactName.Length - 2) : exactName + "es";

        if (ModelState.IsValid)
        {
            // Aggressive duplicate check
            bool exists = await _context.Categories
                .AnyAsync(c =>
                    c.CategoryName.Trim().ToLower() == exactName ||
                    c.CategoryName.Trim().ToLower() == variation1 ||
                    c.CategoryName.Trim().ToLower() == variation2);

            if (exists)
            {
                TempData["CategoryMessage"] = $"A category similar to \"{category.CategoryName}\" already exists.";
                return RedirectToAction(nameof(Index));
            }

            // Persist new category
            _context.Add(category);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        TempData["CategoryMessage"] = "Failed to create category. Please provide a valid name.";
        return RedirectToAction(nameof(Index));
    }

    // POST: CATEGORYS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int categoryId, [Bind("CategoryId,CategoryName")] Category category)
    {
        if (categoryId != category.CategoryId)
        {
            return NotFound();
        }

        ModelState.Remove("Products");

        if (!string.IsNullOrWhiteSpace(category.CategoryName))
        {
            category.CategoryName = category.CategoryName.Trim();
        }

        if (ModelState.IsValid)
        {
            // Check if another category already holds this name
            bool exists = await _context.Categories
                .AnyAsync(c => c.CategoryId != category.CategoryId &&
                               c.CategoryName.ToLower() == category.CategoryName.ToLower());

            if (exists)
            {
                TempData["CategoryMessage"] = $"Another category named {category.CategoryName} already exists.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.Update(category);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Categories.AnyAsync(e => e.CategoryId == category.CategoryId))
                {
                    return NotFound();
                }
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        TempData["CategoryMessage"] = "Failed to update category. Please check your input.";
        return RedirectToAction(nameof(Index));
    }

    // POST: CATEGORYS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? categoryid)
    {
        var category = await _context.Categories.FindAsync(categoryid);
        if (category == null)
        {
            return NotFound();
        }

        try
        {
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["CategoryMessage"] = $"Cannot delete '{category.CategoryName}' because products are assigned to it.";
        }

        return RedirectToAction(nameof(Index));
    }

    private bool CategoryExists(int? categoryid)
    {
        return _context.Categories.Any(e => e.CategoryId == categoryid);
    }
}