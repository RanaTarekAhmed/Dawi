
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

  
        public async Task<IActionResult> Index()
        {
            var collections = await _context.Requests
                .Include(r => r.Product)
                    .ThenInclude(p => p.Pharmacy)
                .Include(r => r.Beneficiary)
                .Where(r => r.RejectionReason == null)
                .OrderBy(r => r.CollectionDate)
                .ToListAsync();

            if (User.IsInRole(Roles.Pharmacist))
            {
                int pharmacistId = int.Parse(User.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

                collections = collections
                    .Where(r => r.Product.Pharmacy != null &&
                                r.Product.Pharmacy.PharmacistId == pharmacistId)
                    .ToList();
            }

            return View(collections);
        }

    
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var collection = await _context.Requests
                .Include(r => r.Product)
                    .ThenInclude(p => p.Pharmacy)
                .Include(r => r.Beneficiary)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (collection == null || collection.RejectionReason != null)
                return NotFound();

            if (!await CanManageCollection(collection))
                return Forbid();

            return View(collection);
        }

     
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Schedule(int id, DateTime collectionDate)
        {
            if (collectionDate <= DateTime.Now)
                return BadRequest("Collection date must be in the future.");

            var request = await _context.Requests
                .Include(r => r.Product)
                    .ThenInclude(p => p.Pharmacy)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null || request.RejectionReason != null)
                return NotFound();

            if (!await CanManageCollection(request))
                return Forbid();

            request.CollectionDate = collectionDate;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Collection scheduled successfully.";

            return RedirectToAction(nameof(Index));
        }

       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id)
        {
            var request = await _context.Requests
                .Include(r => r.Product)
                    .ThenInclude(p => p.Pharmacy)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null || request.RejectionReason != null)
                return NotFound();

            if (!await CanManageCollection(request))
                return Forbid();

            if (request.CollectionDate == null)
                return BadRequest("Schedule the collection before confirming it.");

            request.CollectionDate = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Collection confirmed successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return BadRequest("Rejection reason is required.");

            var request = await _context.Requests
                .Include(r => r.Product)
                    .ThenInclude(p => p.Pharmacy)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null)
                return NotFound();

            if (!await CanManageCollection(request))
                return Forbid();

            if (request.CollectionDate != null || request.IsPaid)
                return BadRequest("This request cannot be rejected.");

            request.RejectionReason = reason.Trim();
            request.Product.Quantity += request.RequestedQuantity;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Request rejected successfully.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> CanManageCollection(Request request)
        {
            if (User.IsInRole(Roles.Admin))
                return true;

            if (!User.IsInRole(Roles.Pharmacist))
                return false;

            int pharmacistId = int.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            return request.Product.Pharmacy != null &&
                   request.Product.Pharmacy.PharmacistId == pharmacistId;
        }
    }
}
