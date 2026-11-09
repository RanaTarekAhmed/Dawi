
using System.Security.Claims;
using Dawy.Data;
using Dawy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
{
    [Authorize(Roles = Roles.Beneficiary)]
    public class PaymentController : Controller
    {
        private readonly DawiContext _context;

        public PaymentController(DawiContext context)
        {
            _context = context;
        }

  
        public async Task<IActionResult> Index()
        {
            int beneficiaryId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var payments = await _context.Requests
                .Include(r => r.Product)
                .Where(r => r.BeneficiaryId == beneficiaryId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(payments);
        }


        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            int beneficiaryId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var payment = await _context.Requests
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r =>
                    r.RequestId == id &&
                    r.BeneficiaryId == beneficiaryId);

            if (payment == null)
                return NotFound();

            return View(payment);
        }

  
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id)
        {
            int beneficiaryId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var payment = await _context.Requests
                .FirstOrDefaultAsync(r =>
                    r.RequestId == id &&
                    r.BeneficiaryId == beneficiaryId);

            if (payment == null)
                return NotFound();

            if (payment.RejectionReason != null)
                return BadRequest("Payment is not available for rejected requests.");

            if (payment.IsPaid)
                return BadRequest("This payment has already been completed.");

            if (payment.CollectionDate == null)
                return BadRequest("Collection must be scheduled before payment.");

            if (payment.FeeAmount <= 0)
                return BadRequest("There is no payment amount due.");

            payment.IsPaid = true;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Payment recorded successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}
