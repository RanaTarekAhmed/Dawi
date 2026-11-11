using Dawy.Data;
using Dawy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
{
    [AllowAnonymous]
    public class CategoryController : Controller
    {
        private static readonly string[] DefaultCategories = { "أدوية", "مستلزمات طبية", "فيتامينات", "أجهزة طبية" };

        private readonly DawiContext _context;

        public CategoryController(DawiContext context)
        {
            _context = context;
        }

        // GET: /Category
        [HttpGet]
        public async Task<IActionResult> Index(string? category, string? search, string? city, string? sortBy)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Pharmacy)
                .Where(p => p.Status == ProductStatus.Approved
                         && p.Quantity > 0
                         && p.ExpiryDate >= today)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category) && category != "الكل")
            {
                query = query.Where(p => p.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmedSearch = search.Trim();
                query = query.Where(p => p.ProductName.Contains(trimmedSearch));
            }

            if (!string.IsNullOrWhiteSpace(city))
            {
                var trimmedCity = city.Trim();
                query = query.Where(p => p.Pharmacy != null && p.Pharmacy.City == trimmedCity);
            }

            query = sortBy switch
            {
                "quantity" => query.OrderByDescending(p => p.Quantity),
                "expiry" => query.OrderBy(p => p.ExpiryDate),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            var products = await query.ToListAsync();

            var categoryCounts = await _context.Products
                .AsNoTracking()
                .Where(p => p.Status == ProductStatus.Approved && p.Quantity > 0 && p.ExpiryDate >= today)
                .GroupBy(p => p.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Category, v => v.Count);

            var availableCities = await _context.Pharmacies
                .AsNoTracking()
                .Where(p => p.IsVerified && !string.IsNullOrEmpty(p.City))
                .Select(p => p.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.Categories = DefaultCategories;
            ViewBag.CategoryCounts = categoryCounts;
            ViewBag.Cities = availableCities;
            ViewBag.CurrentCategory = category;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentCity = city;
            ViewBag.CurrentSort = sortBy;

            return View(products);
        }

        // GET: /Category/Browse?categoryName=أدوية
        [HttpGet]
        public IActionResult Browse(string categoryName)
        {
            return RedirectToAction(nameof(Index), new { category = categoryName });
        }

        // GET: /Category/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var today = DateOnly.FromDateTime(DateTime.Today);

            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Pharmacy)
                .FirstOrDefaultAsync(p =>
                    p.ProductId == id
                    && p.Status == ProductStatus.Approved
                    && p.ExpiryDate >= today);

            if (product == null)
                return NotFound();

            return View(product);
        }
    }
}