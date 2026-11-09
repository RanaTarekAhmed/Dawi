using Dawy.Data;
using Dawy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
{
    public class CategoryController : Controller
    {
        private readonly DawiContext _context;

        public CategoryController(DawiContext context)
        {
            _context = context;
        }

        // GET: /Category/Index (كتالوج المنتجات والبحث والفلترة)
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? category, string? search, string? sortBy)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var query = _context.Products
                .Include(p => p.Pharmacy)
                .Where(p => (p.Status == "Approved" || p.Status == "مقبول")
                            && p.Quantity > 0
                            && p.ExpiryDate > today)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category) && category != "الكل")
            {
                query = query.Where(p => p.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.ProductName.Contains(search));
            }

            query = sortBy switch
            {
                "quantity" => query.OrderByDescending(p => p.Quantity),
                "expiry" => query.OrderBy(p => p.ExpiryDate),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            var products = await query.ToListAsync();

            ViewBag.Categories = new List<string> { "أدوية", "مستلزمات طبية", "فيتامينات", "أجهزة طبية" };
            ViewBag.CurrentCategory = category;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentSort = sortBy;

            return View(products);
        }

        // GET: /Category/Details/5 (تفاصيل المنتج)
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Pharmacy)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null || (product.Status != "Approved" && product.Status != "مقبول"))
            {
                return NotFound();
            }

            return View(product);
        }
    }
}