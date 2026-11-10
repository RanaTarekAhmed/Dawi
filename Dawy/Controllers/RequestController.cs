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

        //                    المستفيد


        // GET: /Request/Create?productId=5
        [HttpGet]
        [Authorize(Roles = Roles.Beneficiary)]
        public async Task<IActionResult> Create(int productId)
        {
            var product = await GetAvailableProductAsync(productId);
            if (product == null)
                return NotFound();

            return View(new RequestViewModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                AvailableQuantity = product.Quantity
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

            model.ProductName = product.ProductName;
            model.AvailableQuantity = product.Quantity;

            if (!ModelState.IsValid)
                return View(model);

            if (model.Quantity > product.Quantity)
            {
                ModelState.AddModelError(nameof(model.Quantity), "The requested quantity is more than the available quantity.");
                return View(model);
            }

            var userId = GetCurrentUserId();

            var alreadyRequested = await _context.Requests.AnyAsync(r =>
                r.ProductId == model.ProductId
                && r.BeneficiaryId == userId
                && r.RejectionReason == null);

            if (alreadyRequested)
            {
                ModelState.AddModelError(string.Empty, "You have already requested this product.");
                return View(model);
            }

            var quantity = model.Quantity;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            // خصم الكمية بشكل آمن: لو اتنين طلبوا آخر كمية مع بعض واحد بس ينجح
            var reserved = await _context.Products
                .Where(p => p.ProductId == model.ProductId && p.Quantity >= quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Quantity, p => p.Quantity - quantity));

            if (reserved == 0)
            {
                ModelState.AddModelError(string.Empty, "Sorry, the requested quantity is no longer available.");
                return View(model);
            }

            _context.Requests.Add(new Request
            {
                BeneficiaryId = userId,
                ProductId = model.ProductId,
                RequestedQuantity = quantity,
                PrescriptionImageUrl = model.PrescriptionImageUrl,
                RequestDate = DateTime.UtcNow,
                FeeAmount = 0,
                IsPaid = false
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "Your request has been submitted successfully.";
            return RedirectToAction(nameof(MyRequests));
        }

        // GET: /Request/MyRequests
        [Authorize(Roles = Roles.Beneficiary)]
        public async Task<IActionResult> MyRequests()
        {
            var userId = GetCurrentUserId();

            var requests = await _context.Requests
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
                .FirstOrDefaultAsync(r => r.RequestId == id && r.BeneficiaryId == userId);

            if (request == null)
                return NotFound();

            // مينفعش يتلغي لو اتسلّم أو اتدفع أو اترفض
            if (request.CollectionDate != null || request.IsPaid || request.RejectionReason != null)
            {
                TempData["Error"] = "This request cannot be cancelled.";
                return RedirectToAction(nameof(MyRequests));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            _context.Requests.Remove(request);
            await _context.SaveChangesAsync();

            await ReturnToStockAsync(request.ProductId, request.RequestedQuantity);

            await transaction.CommitAsync();

            TempData["Success"] = "The request has been cancelled.";
            return RedirectToAction(nameof(MyRequests));
        }


        //                      الصيدلي


        // GET: /Request/PharmacyRequests
        [Authorize(Roles = Roles.Pharmacist)]
        public async Task<IActionResult> PharmacyRequests()
        {
            var pharmacyIds = await GetMyPharmacyIdsAsync();

            var requests = await _context.Requests
                .Include(r => r.Product)
                .Include(r => r.Beneficiary)
                .Where(r => r.Product.PharmacyId.HasValue
                         && pharmacyIds.Contains(r.Product.PharmacyId.Value))
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        // POST: /Request/Collect/5  (تأكيد إن المستفيد استلم الدواء)
        [HttpPost]
        [Authorize(Roles = Roles.Pharmacist)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Collect(int id)
        {
            var request = await GetMyPharmacyRequestAsync(id);
            if (request == null)
                return NotFound();

            if (request.RejectionReason != null || request.CollectionDate != null)
            {
                TempData["Error"] = "This request cannot be marked as collected.";
                return RedirectToAction(nameof(PharmacyRequests));
            }

            request.CollectionDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = "The collection has been recorded.";
            return RedirectToAction(nameof(PharmacyRequests));
        }

        // POST: /Request/Reject/5  (رفض الطلب بسبب)
        [HttpPost]
        [Authorize(Roles = Roles.Pharmacist)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please enter the rejection reason.";
                return RedirectToAction(nameof(PharmacyRequests));
            }

            var request = await GetMyPharmacyRequestAsync(id);
            if (request == null)
                return NotFound();

            if (request.RejectionReason != null || request.CollectionDate != null)
            {
                TempData["Error"] = "This request cannot be rejected.";
                return RedirectToAction(nameof(PharmacyRequests));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            request.RejectionReason = reason.Trim();
            await _context.SaveChangesAsync();

            await ReturnToStockAsync(request.ProductId, request.RequestedQuantity);

            await transaction.CommitAsync();

            TempData["Success"] = "The request has been rejected.";
            return RedirectToAction(nameof(PharmacyRequests));
        }

        // ---------------------- Helpers ----------------------

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        private async Task<Product?> GetAvailableProductAsync(int productId)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            return await _context.Products.AsNoTracking().FirstOrDefaultAsync(p =>
                p.ProductId == productId
                && p.Status == ProductStatus.Approved
                && p.Quantity > 0
                && p.ExpiryDate >= today);
        }

        private async Task<List<int>> GetMyPharmacyIdsAsync()
        {
            var userId = GetCurrentUserId();

            return await _context.Pharmacies
                .Where(p => p.PharmacistId == userId)
                .Select(p => p.PharmacyId)
                .ToListAsync();
        }

        // الطلب لازم يكون على دواء تابع لصيدلية الصيدلي ده
        private async Task<Request?> GetMyPharmacyRequestAsync(int requestId)
        {
            var pharmacyIds = await GetMyPharmacyIdsAsync();

            return await _context.Requests
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r =>
                    r.RequestId == requestId
                    && r.Product.PharmacyId.HasValue
                    && pharmacyIds.Contains(r.Product.PharmacyId.Value));
        }

        private async Task ReturnToStockAsync(int productId, int quantity)
        {
            await _context.Products
                .Where(p => p.ProductId == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Quantity, p => p.Quantity + quantity));
        }
    }
}