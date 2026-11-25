using System.Security.Claims;
using Dawi.Data;
using Dawi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawi.Controllers
{
    [Authorize(Roles = Roles.Beneficiary)]
    public class PaymentController : Controller
    {
        private readonly DawiContext _context;

        public PaymentController(DawiContext context)
        {
            _context = context;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public async Task<IActionResult> Index()
        {
            var userId = CurrentUserId;

            var payments = await _context.Requests
                .AsNoTracking()
                .Include(r => r.Product)
                .Where(r => r.BeneficiaryId == userId && r.FeeAmount > 0)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(payments);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var userId = CurrentUserId;

            var payment = await _context.Requests
                .AsNoTracking()
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r => r.RequestId == id && r.BeneficiaryId == userId);

            if (payment == null)
                return NotFound();

            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id)
        {
            var userId = CurrentUserId;

            var updated = await _context.Requests
                .Where(r => r.RequestId == id
                         && r.BeneficiaryId == userId
                         && !r.IsPaid
                         && r.RejectionReason == null
                         && r.CollectionDate != null
                         && r.CollectedAt == null
                         && r.FeeAmount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsPaid, true));

            if (updated > 0)
            {
                TempData["SuccessMessage"] = "Payment recorded successfully.";
                return RedirectToAction(nameof(Index));
            }

            // مفيش صف اتحدّث: نعرف السبب بالظبط
            var request = await _context.Requests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RequestId == id && r.BeneficiaryId == userId);

            if (request == null)
                return NotFound();

            TempData["ErrorMessage"] = request switch
            {
                { RejectionReason: not null } => "Payment is not available for rejected requests.",
                { IsPaid: true } => "This request has already been paid.",
                { CollectedAt: not null } => "This request has already been collected.",
                { CollectionDate: null } => "The pharmacist must schedule the collection before payment.",
                { FeeAmount: <= 0 } => "There is no fee due for this request.",
                _ => "The payment could not be recorded. Please try again."
            };

            return RedirectToAction(nameof(Index));
        }
    }
}


