using System.Security.Claims;
using Dawi.Data;
using Dawi.Models;
using Dawi.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawi.Controllers
{
    public class ProductController : Controller
    {
        private const int MaxReasonLength = 500;
        private static readonly string[] ProhibitedKeywords = { "انسولين", "أنسولين", "insulin", "مطعوم", "لقاح", "vaccine" };

        private readonly DawiContext _context;

        public ProductController(DawiContext context)
        {
            _context = context;
        }

        // GET: /Product
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? search)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var query = _context.Products
                .Where(p => p.Status == ProductStatus.Approved
                         && p.Quantity > 0
                         && p.ExpiryDate >= today);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.ProductName.Contains(search));

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.Search = search;
            return View(products);
        }

        // GET: /Product/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Pharmacy)
                .FirstOrDefaultAsync(p => p.ProductId == id && p.Status == ProductStatus.Approved);

            if (product == null)
                return NotFound();

            return View(product);
        }

        // POST: /Product/Create
        [HttpPost]
        [Authorize(Roles = Roles.Donor)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (ProhibitedKeywords.Any(k =>
         model.ProductName.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(model.ProductName),
                    "This item cannot be donated (cold-chain or controlled).");
                return BadRequest(ModelState);
            }

            if (model.ExpiryDate.Date < DateTime.Today.AddMonths(3))
            {
                ModelState.AddModelError(nameof(model.ExpiryDate), "Expiry date must be at least 3 months from today.");
                return BadRequest(ModelState);
            }

            var pharmacyExists = await _context.Pharmacies
                .AnyAsync(p => p.PharmacyId == model.PharmacyId && p.IsVerified);

            if (!pharmacyExists)
            {
                ModelState.AddModelError(nameof(model.PharmacyId), "Pharmacy not found.");
                return BadRequest(ModelState);
            }

            var product = new Product
            {
                DonorId = GetCurrentUserId(),
                ProductName = model.ProductName.Trim(),
                Category = model.Category,
                Quantity = model.Quantity,
                ExpiryDate = DateOnly.FromDateTime(model.ExpiryDate),
                PharmacyId = model.PharmacyId,
                Status = ProductStatus.AdminPending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            var adminIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == Roles.Admin && u.IsActive)
                .Select(u => u.UserId)
                .ToListAsync();
            foreach (var adminId in adminIds)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = adminId,
                    Title = "تبرع جديد ينتظر المراجعة الإدارية",
                    Message = $"تم التبرع بـ '{product.ProductName}'. يرجى المراجعة قبل إحالته للصيدلي.",
                    Link = "/Admin/Products",
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyDonations));
        }

        // GET: /Product/MyDonations
        [Authorize(Roles = Roles.Donor)]
        public async Task<IActionResult> MyDonations()
        {
            var userId = GetCurrentUserId();

            var products = await _context.Products
                .Where(p => p.DonorId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(products);
        }

        // GET: /Product/Incoming
        [Authorize(Roles = Roles.Pharmacist)]
        public async Task<IActionResult> Incoming()
        {
            var pharmacyIds = await GetMyPharmacyIdsAsync();

            var products = await _context.Products
                .Where(p => p.Status == ProductStatus.Pending
                         && p.PharmacyId.HasValue
                         && pharmacyIds.Contains(p.PharmacyId.Value))
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            return View(products);
        }

        // POST: /Product/Approve/5
        [HttpPost]
        [Authorize(Roles = Roles.Pharmacist)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var product = await GetMyPendingProductAsync(id);
            if (product == null)
                return NotFound();

            if (product.ExpiryDate < DateOnly.FromDateTime(DateTime.Today))
                product.Status = ProductStatus.Rejected;
            else
                product.Status = ProductStatus.Approved;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Incoming));
        }

        // POST: /Product/Reject/5

        [HttpPost]
        [Authorize(Roles = Roles.Pharmacist)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? reason = null)
        {
            reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

            if (reason?.Length > MaxReasonLength)
            {
                TempData["Error"] = $"The rejection reason must not exceed {MaxReasonLength} characters.";
                return RedirectToAction(nameof(Incoming));
            }

            var product = await GetMyPendingProductAsync(id);
            if (product == null)
                return NotFound();

            if (!await SetPendingStatusAsync(id, ProductStatus.Rejected, reason))
                TempData["Error"] = "This donation was already processed.";

            return RedirectToAction(nameof(Incoming));
        }

        // ---------------------- Helpers ----------------------

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        private async Task<List<int>> GetMyPharmacyIdsAsync()
        {
            var userId = GetCurrentUserId();

            return await _context.Pharmacies
                .Where(p => p.PharmacistId == userId)
                .Select(p => p.PharmacyId)
                .ToListAsync();
        }

        private async Task<Product?> GetMyPendingProductAsync(int id)
        {
            var pharmacyIds = await GetMyPharmacyIdsAsync();

            return await _context.Products.FirstOrDefaultAsync(p =>
                p.ProductId == id
                && p.Status == ProductStatus.Pending
                && p.PharmacyId.HasValue
                && pharmacyIds.Contains(p.PharmacyId.Value));
        }

        private async Task<bool> SetPendingStatusAsync(int id, string status, string? reason)
        {
            var changed = await _context.Products
                .Where(p => p.ProductId == id && p.Status == ProductStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, status)
                    .SetProperty(p => p.RejectionReason, reason));

            return changed > 0;
        }
    }
}

