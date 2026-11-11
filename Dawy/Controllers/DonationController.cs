using System.Security.Claims;
using Dawy.Data;
using Dawy.Models;
using Dawy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
{
    [Authorize]
    public class DonationController : Controller
    {
        private static readonly string[] ProhibitedKeywords = { "انسولين", "أنسولين", "insulin", "مطعوم", "لقاح", "vaccine", "مخدر", "جدول" };

        private readonly DawiContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public DonationController(DawiContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // GET: /Donation
        [HttpGet]
        [Authorize(Roles = Roles.Donor + "," + Roles.Admin)]
        public async Task<IActionResult> Index(string? status)
        {
            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Pharmacy)
                .Include(p => p.Donor)
                .AsQueryable();

            if (User.IsInRole(Roles.Donor))
            {
                query = query.Where(p => p.DonorId == CurrentUserId);
            }

            var allUserDonations = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.TotalDonations = allUserDonations.Count;
            ViewBag.ApprovedCount = allUserDonations.Count(p => p.Status == ProductStatus.Approved);
            ViewBag.PendingCount = allUserDonations.Count(p => p.Status == ProductStatus.Pending);
            ViewBag.RejectedCount = allUserDonations.Count(p => p.Status == ProductStatus.Rejected);

            var filteredDonations = string.IsNullOrWhiteSpace(status)
                ? allUserDonations
                : allUserDonations.Where(p => p.Status == status).ToList();

            ViewBag.CurrentStatus = status;
            return View(filteredDonations);
        }

        // GET: /Donation/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Pharmacy)
                .Include(p => p.Donor)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            if (User.IsInRole(Roles.Donor) && product.DonorId != CurrentUserId)
                return Forbid();

            return View(product);
        }

        // GET: /Donation/Create
        [HttpGet]
        [Authorize(Roles = Roles.Donor)]
        public async Task<IActionResult> Create()
        {
            await LoadDonationFormLookupsAsync();
            return View(new ProductViewModel
            {
                ExpiryDate = DateTime.Today.AddMonths(6),
                Quantity = 1
            });
        }

        // POST: /Donation/Create
        [HttpPost]
        [Authorize(Roles = Roles.Donor)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductViewModel model, IFormFile? imageFile)
        {
            if (!string.IsNullOrWhiteSpace(model.ProductName) &&
                ProhibitedKeywords.Any(k => model.ProductName.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(model.ProductName),
                    "عذراً، لا يمكن التبرع بأدوية الثلاجة (مثل الأنسولين واللقاحات) أو أدوية الجدول.");
            }

            if (model.ExpiryDate.Date < DateTime.Today.AddMonths(3))
            {
                ModelState.AddModelError(nameof(model.ExpiryDate),
                    "يجب أن يكون تاريخ الصلاحية سارياً لمدة 3 أشهر على الأقل من تاريخ اليوم.");
            }

            var pharmacy = await _context.Pharmacies
                .FirstOrDefaultAsync(p => p.PharmacyId == model.PharmacyId && p.IsVerified);

            if (pharmacy == null)
            {
                ModelState.AddModelError(nameof(model.PharmacyId), "يرجى اختيار صيدلية معتمدة للمراجعة.");
            }

            if (!ModelState.IsValid)
            {
                await LoadDonationFormLookupsAsync(model.PharmacyId);
                return View(model);
            }

            string? imageUrl = null;
            if (imageFile != null && imageFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(ext))
                {
                    ModelState.AddModelError("imageFile", "يرجى رفع صورة بصيغة صحيحة (JPG, PNG, WEBP).");
                    await LoadDonationFormLookupsAsync(model.PharmacyId);
                    return View(model);
                }

                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = $"{Guid.NewGuid()}{ext}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                await using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                imageUrl = "/images/products/" + uniqueFileName;
            }

            var product = new Product
            {
                DonorId = CurrentUserId,
                ProductName = model.ProductName.Trim(),
                Category = model.Category.Trim(),
                Quantity = model.Quantity,
                ExpiryDate = DateOnly.FromDateTime(model.ExpiryDate),
                ProductImageUrl = imageUrl,
                PharmacyId = model.PharmacyId,
                Status = ProductStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            await WriteAuditAsync("DonationCreated", nameof(Product), product.ProductId, $"Product: {product.ProductName}, Qty: {product.Quantity}");

            if (pharmacy!.PharmacistId.HasValue)
            {
                await NotifyAsync(
                    pharmacy.PharmacistId.Value,
                    "تبرع جديد قيد المراجعة",
                    $"تم إرسال تبرع جديد ({product.ProductName}) للمراجعة في صيدليتك.",
                    "/Inspection");
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إرسال تبرعك بنجاح وهو الآن قيد المراجعة لدى الصيدلية.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Donation/Cancel/5
        [HttpPost]
        [Authorize(Roles = Roles.Donor)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == id && p.DonorId == CurrentUserId);

            if (product == null)
                return NotFound();

            if (product.Status != ProductStatus.Pending)
            {
                TempData["ErrorMessage"] = "لا يمكن إلغاء التبرع بعد مراجعته من الصيدلية.";
                return RedirectToAction(nameof(Index));
            }

            _context.Products.Remove(product);
            await WriteAuditAsync("DonationCancelled", nameof(Product), id, $"Cancelled by donor: {product.ProductName}");
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إلغاء التبرع بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadDonationFormLookupsAsync(int? selectedPharmacyId = null)
        {
            var verifiedPharmacies = await _context.Pharmacies
                .AsNoTracking()
                .Where(p => p.IsVerified)
                .OrderBy(p => p.PharmacyName)
                .ToListAsync();

            ViewBag.Pharmacies = new SelectList(verifiedPharmacies, "PharmacyId", "PharmacyName", selectedPharmacyId);
            ViewBag.Categories = new List<string> { "أدوية", "مستلزمات طبية", "فيتامينات", "أجهزة طبية" };
        }

        private Task NotifyAsync(int userId, string title, string message, string? link) =>
            _context.Notifications.AddAsync(new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Link = link,
                CreatedAt = DateTime.UtcNow
            }).AsTask();

        private Task WriteAuditAsync(string action, string entityName, int entityId, string? details) =>
            _context.AuditLogs.AddAsync(new AuditLog
            {
                UserId = CurrentUserId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId.ToString(),
                Details = details,
                CreatedAt = DateTime.UtcNow
            }).AsTask();
    }
}