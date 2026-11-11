using System.Security.Claims;
using Dawy.Data;
using Dawy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
{
    [Authorize(Roles = Roles.Pharmacist + "," + Roles.Admin)]
    public class InspectionController : Controller
    {
        private readonly DawiContext _context;

        public InspectionController(DawiContext context)
        {
            _context = context;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // GET: /Inspection
        [HttpGet]
        public async Task<IActionResult> Index(string? status)
        {
            var currentFilter = string.IsNullOrWhiteSpace(status) ? ProductStatus.Pending : status;

            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Donor)
                .Include(p => p.Pharmacy)
                .AsQueryable();

            if (User.IsInRole(Roles.Pharmacist))
            {
                var myPharmacyIds = await GetMyPharmacyIdsAsync();
                query = query.Where(p => p.PharmacyId.HasValue && myPharmacyIds.Contains(p.PharmacyId.Value));
            }

            var allScopedProducts = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.PendingCount = allScopedProducts.Count(p => p.Status == ProductStatus.Pending);
            ViewBag.ApprovedCount = allScopedProducts.Count(p => p.Status == ProductStatus.Approved);
            ViewBag.RejectedCount = allScopedProducts.Count(p => p.Status == ProductStatus.Rejected);
            ViewBag.CurrentStatus = currentFilter;

            var filteredList = currentFilter == "All"
                ? allScopedProducts
                : allScopedProducts.Where(p => p.Status == currentFilter).ToList();

            return View(filteredList);
        }

        // GET: /Inspection/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Donor)
                .Include(p => p.Pharmacy)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            if (!await CanInspectProductAsync(product))
                return Forbid();

            return View(product);
        }

        // POST: /Inspection/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, int? verifiedQuantity, string? notes)
        {
            var product = await _context.Products
                .Include(p => p.Pharmacy)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            if (!await CanInspectProductAsync(product))
                return Forbid();

            if (product.Status != ProductStatus.Pending)
            {
                TempData["ErrorMessage"] = "تمت مراجعة هذا التبرع مسبقاً.";
                return RedirectToAction(nameof(Index));
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            if (product.ExpiryDate <= today)
            {
                product.Status = ProductStatus.Rejected;
                await WriteAuditAsync("InspectionAutoRejectedExpired", nameof(Product), product.ProductId, "Expired product rejected during inspection.");
                await NotifyAsync(product.DonorId, "تحديث حالة التبرع", $"عذراً، تم رفض تبرعك '{product.ProductName}' لانتهاء تاريخ الصلاحية.", "/Donation");
                await _context.SaveChangesAsync();

                TempData["ErrorMessage"] = "الدواء منتهي الصلاحية وتم تحويله إلى مرفوض تلقائياً.";
                return RedirectToAction(nameof(Index));
            }

            if (verifiedQuantity.HasValue && verifiedQuantity.Value > 0)
            {
                product.Quantity = verifiedQuantity.Value;
            }

            product.Status = ProductStatus.Approved;

            await WriteAuditAsync(
                "InspectionApproved",
                nameof(Product),
                product.ProductId,
                $"Approved by pharmacist. Qty={product.Quantity}. Notes={notes}");

            await NotifyAsync(
                product.DonorId,
                "تم قبول تبرعك",
                $"شكراً لك! تم فحص وقبول تبرعك '{product.ProductName}' في الصيدلية وهو متاح الآن للمستفيدين.",
                "/Donation");

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم فحص الدواء واعتماده بنجاح في الكتالوج.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Inspection/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["ErrorMessage"] = "يرجى كتابة سبب رفض التبرع لإبلاغ المتبرع.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var product = await _context.Products
                .Include(p => p.Pharmacy)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            if (!await CanInspectProductAsync(product))
                return Forbid();

            if (product.Status != ProductStatus.Pending)
            {
                TempData["ErrorMessage"] = "تمت مراجعة هذا التبرع مسبقاً.";
                return RedirectToAction(nameof(Index));
            }

            product.Status = ProductStatus.Rejected;
            var cleanReason = reason.Trim();

            await WriteAuditAsync(
                "InspectionRejected",
                nameof(Product),
                product.ProductId,
                $"Reason: {cleanReason}");

            await NotifyAsync(
                product.DonorId,
                "تم رفض التبرع بعد الفحص",
                $"نعتذر، لم يتم قبول تبرعك '{product.ProductName}'. السبب: {cleanReason}",
                "/Donation");

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم رفض التبرع وإرسال إشعار بالسبب للمتبرع.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<int>> GetMyPharmacyIdsAsync()
        {
            return await _context.Pharmacies
                .AsNoTracking()
                .Where(p => p.PharmacistId == CurrentUserId)
                .Select(p => p.PharmacyId)
                .ToListAsync();
        }

        private async Task<bool> CanInspectProductAsync(Product product)
        {
            if (User.IsInRole(Roles.Admin))
                return true;

            if (!User.IsInRole(Roles.Pharmacist) || !product.PharmacyId.HasValue)
                return false;

            var myPharmacyIds = await GetMyPharmacyIdsAsync();
            return myPharmacyIds.Contains(product.PharmacyId.Value);
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