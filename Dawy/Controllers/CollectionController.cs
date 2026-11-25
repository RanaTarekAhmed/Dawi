using System.Security.Claims;
using Dawi.Data;
using Dawi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawi.Controllers
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

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var requests = await ManageableRequests()
                .AsNoTracking()
                .Where(r => r.RejectionReason == null && r.CollectedAt == null)
                .OrderBy(r => r.CollectionDate ?? DateTime.MaxValue)
                .ToListAsync();

            return View(requests);
        }

        [HttpGet]
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
                return Back("يجب أن يكون موعد الاستلام في المستقبل.");

            if (fee < 0)
                return Back("لا يمكن أن تكون الرسوم قيمة سالبة.");

            var request = await ManageableRequests().FirstOrDefaultAsync(r => r.RequestId == id);
            if (request == null)
                return NotFound();

            if (request.RejectionReason != null || request.CollectedAt != null)
                return Back("لا يمكن تعديل موعد الاستلام لهذا الطلب.");

            request.CollectionDate = collectionDate;
            request.Status = RequestStatus.Approved;

            // مينفعش الرسوم تتغير بعد الدفع
            if (!request.IsPaid)
                request.FeeAmount = fee;

            _context.Notifications.Add(new Notification
            {
                UserId = request.BeneficiaryId,
                Title = "تم تحديد موعد الاستلام",
                Message = $"موعد استلام ({request.Product.ProductName}): {collectionDate:yyyy-MM-dd HH:mm}",
                Link = "/Request/MyRequests",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تحديد موعد الاستلام والرسوم بنجاح.";
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
                return Back("لا يمكن تأكيد استلام هذا الطلب.");

            if (request.FeeAmount > 0 && !request.IsPaid)
                return Back("لم يتم دفع الرسوم المطلوبة لهذا الطلب بعد.");

            request.CollectedAt = DateTime.UtcNow;
            request.Status = RequestStatus.Collected;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تأكيد تسليم الدواء بنجاح.";
            return RedirectToAction(nameof(Index));
        }
    }
}

