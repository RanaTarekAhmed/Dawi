
using System.Security.Claims;
using Dawy.Data;
using Dawy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
{
    [Authorize(Roles = Roles.Beneficiary)]
    public class OrderController : Controller
    {
        private readonly DawiContext _context;

        public OrderController(DawiContext context)
        {
            _context = context;
        }

    
        public async Task<IActionResult> Index()
        {
            int beneficiaryId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var orders = await _context.Requests
                .Include(r => r.Product)
                .Where(r => r.BeneficiaryId == beneficiaryId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            int beneficiaryId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var order = await _context.Requests
                .Include(r => r.Product)
                .Include(r => r.Beneficiary)
                .FirstOrDefaultAsync(r =>
                    r.RequestId == id &&
                    r.BeneficiaryId == beneficiaryId);

            if (order == null)
                return NotFound();

            return View(order);
        }

     
        [HttpGet]
        public async Task<IActionResult> Create(int? id)
        {
            if (id == null)
                return NotFound();

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null ||
                (product.Status != "Approved" && product.Status != "مقبول") ||
                product.Quantity <= 0 ||
                product.ExpiryDate <= DateOnly.FromDateTime(DateTime.Today))
            {
                return NotFound();
            }

            ViewBag.Product = product;

            return View();
        }

    
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int productId, int quantity)
        {
            if (quantity <= 0)
            {
                ModelState.AddModelError("quantity", "Quantity must be greater than zero.");
                return BadRequest(ModelState);
            }

            int beneficiaryId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
                return NotFound();

            if (product.Status != "Approved" && product.Status != "مقبول")
                return BadRequest("This product is not available.");

            if (product.ExpiryDate <= DateOnly.FromDateTime(DateTime.Today))
                return BadRequest("This product has expired.");

            if (product.Quantity < quantity)
                return BadRequest("Requested quantity is not available.");

            product.Quantity -= quantity;

            var order = new Request
            {
                BeneficiaryId = beneficiaryId,
                ProductId = productId,
                RequestedQuantity = quantity,
                RequestDate = DateTime.Now,
                FeeAmount = 0,
                IsPaid = false
            };

            _context.Requests.Add(order);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your order has been submitted successfully.";

            return RedirectToAction(nameof(Index));
        }

  
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            int beneficiaryId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var order = await _context.Requests
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r =>
                    r.RequestId == id &&
                    r.BeneficiaryId == beneficiaryId);

            if (order == null)
                return NotFound();

            if (order.CollectionDate != null || order.IsPaid)
                return BadRequest("This order cannot be cancelled.");

            order.Product.Quantity += order.RequestedQuantity;

            _context.Requests.Remove(order);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Order cancelled successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}
