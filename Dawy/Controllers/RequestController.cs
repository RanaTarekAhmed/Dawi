using System.Security.Claims;
using Dawy.Data;
using Dawy.Models;
using Dawy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
{
    [Authorize]
    public class RequestController : Controller
    {
        private readonly DawiContext _context;

        public RequestController(DawiContext context)
        {
            _context = context;
        }

        // GET: /Request/Create?productId=5
        [HttpGet]
        [Authorize(Roles = Roles.Beneficiary)]
        public async Task<IActionResult> Create(int productId)
        {
            var product = await GetAvailableProductAsync(productId);
            if (product == null)
                return NotFound();

            ViewBag.Product = product;

            return View(new RequestViewModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Category = product.Category,
                AvailableQuantity = product.Quantity,
                Quantity = 1
            });
        }

        // POST: /Request/Create
        [HttpPost]
        [Authorize(Roles = Roles.Beneficiary)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RequestViewModel model)
        {
            var product = await GetAvailableProductAsync(model.ProductId);
            if (product == null)
                return NotFound();

            // The view needs the product again whenever validation fails.
            ViewBag.Product = product;
            model.ProductName = product.ProductName;
            model.Category = product.Category;
            model.AvailableQuantity = product.Quantity;

            if (product.Category == "أدوية" &&
                string.IsNullOrWhiteSpace(model.PrescriptionImageUrl))
            {
                ModelState.AddModelError(
                    nameof(model.PrescriptionImageUrl),
                    "صورة الروشتة مطلوبة للأدوية.");
            }

            if (!ModelState.IsValid)
                return View(model);

            if (model.Quantity > product.Quantity)
            {
                ModelState.AddModelError(
                    nameof(model.Quantity),
                    "الكمية المطلوبة أكبر من الكمية المتاحة.");
                return View(model);
            }

            var userId = GetCurrentUserId();

            var alreadyRequested = await _context.Requests.AnyAsync(r =>
                r.ProductId == model.ProductId &&
                r.BeneficiaryId == userId &&
                (r.Status == RequestStatus.Pending ||
                 r.Status == RequestStatus.Approved));

            if (alreadyRequested)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "لديك طلب قائم بالفعل على هذا المنتج.");
                return View(model);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Atomic stock reservation prevents two beneficiaries from taking the last quantity.
            var reserved = await _context.Products
                .Where(p => p.ProductId == model.ProductId &&
                            p.Status == ProductStatus.Approved &&
                            p.Quantity >= model.Quantity)
                .ExecuteUpdateAsync(update => update.SetProperty(
                    p => p.Quantity, p => p.Quantity - model.Quantity));

            if (reserved == 0)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(
                    nameof(model.Quantity),
                    "عذراً، الكمية المطلوبة لم تعد متاحة.");
                product = await GetAvailableProductAsync(model.ProductId);
                if (product == null)
                    return NotFound();

                ViewBag.Product = product;
                model.AvailableQuantity = product.Quantity;
                return View(model);
            }

            _context.Requests.Add(new Request
            {
                BeneficiaryId = userId,
                ProductId = model.ProductId,
                RequestedQuantity = model.Quantity,
                PrescriptionImageUrl = model.PrescriptionImageUrl,
                RequestDate = DateTime.UtcNow,
                Status = RequestStatus.Pending,
                FeeAmount = 0,
                IsPaid = false
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "تم إرسال طلبك بنجاح.";
            return RedirectToAction(nameof(MyRequests));
        }

        // GET: /Request/MyRequests
        [HttpGet]
        [Authorize(Roles = Roles.Beneficiary)]
        public async Task<IActionResult> MyRequests()
        {
            var userId = GetCurrentUserId();

            var requests = await _context.Requests
                .AsNoTracking()
                .Include(r => r.Product)
                    .ThenInclude(p => p.Pharmacy)
                .Where(r => r.BeneficiaryId == userId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        // POST: /Request/Cancel/5
        [HttpPost]
        [Authorize(Roles = Roles.Beneficiary)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = GetCurrentUserId();

            var request = await _context.Requests
                .FirstOrDefaultAsync(r =>
                    r.RequestId == id && r.BeneficiaryId == userId);

            if (request == null)
                return NotFound();

            if ((request.Status != RequestStatus.Pending &&
                 request.Status != RequestStatus.Approved) ||
                request.CollectionDate != null ||
                request.CollectedAt != null ||
                request.IsPaid)
            {
                TempData["Error"] = "لا يمكن إلغاء هذا الطلب.";
                return RedirectToAction(nameof(MyRequests));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            request.Status = RequestStatus.Cancelled;
            await ReturnToStockAsync(request.ProductId, request.RequestedQuantity);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "تم إلغاء الطلب وإرجاع الكمية للمخزون.";
            return RedirectToAction(nameof(MyRequests));
        }

        // GET: /Request/PharmacyRequests
        [HttpGet]
        [Authorize(Roles = Roles.Pharmacist)]
        public async Task<IActionResult> PharmacyRequests()
        {
            var pharmacyIds = await GetMyPharmacyIdsAsync();

            var requests = await _context.Requests
                .AsNoTracking()
                .Include(r => r.Product)
                    .ThenInclude(p => p.Pharmacy)
                .Include(r => r.Beneficiary)
                .Where(r => r.Product.PharmacyId.HasValue &&
                            pharmacyIds.Contains(r.Product.PharmacyId.Value))
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        // POST: /Request/ConfirmPickup/5
        [HttpPost]
        [Authorize(Roles = Roles.Pharmacist)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPickup(int id)
        {
            var request = await GetMyPharmacyRequestAsync(id);
            if (request == null)
                return NotFound();

            if (request.Status != RequestStatus.Approved &&
                request.Status != RequestStatus.Pending)
            {
                TempData["Error"] = "لا يمكن تأكيد استلام هذا الطلب.";
                return RedirectToAction(nameof(PharmacyRequests));
            }

            request.Status = RequestStatus.Collected;
            request.CollectionDate = DateTime.UtcNow;
            request.CollectedAt = request.CollectionDate;

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم تأكيد تسليم الطلب للمستفيد.";
            return RedirectToAction(nameof(PharmacyRequests));
        }

        // POST: /Request/Reject/5
        [HttpPost]
        [Authorize(Roles = Roles.Pharmacist)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "يرجى كتابة سبب الرفض.";
                return RedirectToAction(nameof(PharmacyRequests));
            }

            var request = await GetMyPharmacyRequestAsync(id);
            if (request == null)
                return NotFound();

            if (request.Status != RequestStatus.Pending &&
                request.Status != RequestStatus.Approved)
            {
                TempData["Error"] = "لا يمكن رفض هذا الطلب.";
                return RedirectToAction(nameof(PharmacyRequests));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var cleanReason = reason.Trim();
            request.Status = RequestStatus.Rejected;
            request.RejectionReason = cleanReason;

            await ReturnToStockAsync(request.ProductId, request.RequestedQuantity);

            _context.Notifications.Add(new Notification
            {
                UserId = request.BeneficiaryId,
                Title = "تم رفض طلبك",
                Message = $"تم رفض طلبك للمنتج '{request.Product.ProductName}'. السبب: {cleanReason}",
                Link = "/Request/MyRequests",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "تم رفض الطلب وإرجاع الكمية للمخزون وإرسال إشعار للمستفيد.";
            return RedirectToAction(nameof(PharmacyRequests));
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        private async Task<Product?> GetAvailableProductAsync(int productId)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            return await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.ProductId == productId &&
                    p.Status == ProductStatus.Approved &&
                    p.Quantity > 0 &&
                    p.ExpiryDate >= today);
        }

        private async Task<List<int>> GetMyPharmacyIdsAsync()
        {
            var userId = GetCurrentUserId();

            return await _context.Pharmacies
                .Where(p => p.PharmacistId == userId)
                .Select(p => p.PharmacyId)
                .ToListAsync();
        }

        private async Task<Request?> GetMyPharmacyRequestAsync(int requestId)
        {
            var pharmacyIds = await GetMyPharmacyIdsAsync();

            return await _context.Requests
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r =>
                    r.RequestId == requestId &&
                    r.Product.PharmacyId.HasValue &&
                    pharmacyIds.Contains(r.Product.PharmacyId.Value));
        }

        private async Task ReturnToStockAsync(int productId, int quantity)
        {
            await _context.Products
                .Where(p => p.ProductId == productId)
                .ExecuteUpdateAsync(update => update.SetProperty(
                    p => p.Quantity, p => p.Quantity + quantity));
        }
    }
}
