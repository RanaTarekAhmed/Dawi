using System.Security.Claims;
using Dawy.Data;
using Dawy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
{
    [Authorize(Roles = Roles.Pharmacist + "," + Roles.Admin)]
    public class CollectionController : Controller
    {
        private readonly DawiContext _context;

        public CollectionController(DawiContext context)
        {
            _context = context;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // الأدمن يشوف الكل، والصيدلي يشوف طلبات صيدلياته بس
        private IQueryable<Request> ManageableRequests()
        {
            var query = _context.Requests
                .Include(r => r.Product).ThenInclude(p => p.Pharmacy)
                .Include(r => r.Beneficiary)
                .AsQueryable();

            if (User.IsInRole(Roles.Admin))
                return query;

            var userId = CurrentUserId;
            return query.Where(r => r.Product.Pharmacy != null
                                 && r.Product.Pharmacy.PharmacistId == userId);
        }

        private IActionResult Back(string error)
        {
            TempData["ErrorMessage"] = error;
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Index()
        {
            var requests = await ManageableRequests()
                .AsNoTracking()
                .Where(r => r.RejectionReason == null && r.CollectedAt == null)
                .OrderBy(r => r.CollectionDate ?? DateTime.MaxValue)
                .ToListAsync();

            return View(requests);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await ManageableRequests()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null)
                return NotFound();

            return View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Schedule(int id, DateTime collectionDate, decimal fee)
        {
            if (collectionDate <= DateTime.Now)
                return Back("The collection date must be in the future.");

            if (fee < 0)
                return Back("The fee cannot be negative.");

            var request = await ManageableRequests().FirstOrDefaultAsync(r => r.RequestId == id);
            if (request == null)
                return NotFound();

            if (request.RejectionReason != null || request.CollectedAt != null)
                return Back("The collection date of this request cannot be changed");

            request.CollectionDate = collectionDate;

            // مينفعش الرسوم تتغير بعد الدفع
            if (!request.IsPaid)
                request.FeeAmount = fee;

            _context.Notifications.Add(new Notification
            {
                UserId = request.BeneficiaryId,
                Title = "Collection date set",
                Message = $"Collection date for ({request.Product.ProductName}): {collectionDate:yyyy-MM-dd HH:mm}",
                Link = "/Request/MyRequests",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Collection date set successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id)
        {
            var request = await ManageableRequests().FirstOrDefaultAsync(r => r.RequestId == id);
            if (request == null)
                return NotFound();

            if (request.RejectionReason != null || request.CollectedAt != null)
                return Back("The collection of this request cannot be confirmed.");

            if (request.FeeAmount > 0 && !request.IsPaid)
                return Back("The fee has not been paid yet.");

            request.CollectedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Collection recorded successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
