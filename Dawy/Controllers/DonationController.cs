using Dawy.Data;
using Dawy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
{
    [Authorize]
    public class DonationController : Controller
    {
        private readonly DawiContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public DonationController(DawiContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: /Donation
        public async Task<IActionResult> Index()
        {
            var donations = await _context.Products
                .Include(p => p.Pharmacy)
                .Include(p => p.Donor)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.TotalDonations = donations.Count;
            ViewBag.ApprovedCount = donations.Count(p => p.Status == "Approved" || p.Status == "مقبول");
            ViewBag.PendingCount = donations.Count(p => p.Status == "Pending" || p.Status == "قيد المراجعة");
            ViewBag.RejectedCount = donations.Count(p => p.Status == "Rejected" || p.Status == "مرفوض");

            return View(donations);
        }

        // GET: /Donation/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var verifiedPharmacies = await _context.Pharmacies
                .Where(p => p.IsVerified)
                .ToListAsync();

            ViewBag.Pharmacies = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(verifiedPharmacies, "PharmacyId", "PharmacyName");
            ViewBag.Categories = new List<string> { "أدوية", "مستلزمات طبية", "فيتامينات", "أجهزة طبية" };

            return View();
        }

        // POST: /Donation/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            ModelState.Remove("Donor");
            ModelState.Remove("Pharmacy");
            ModelState.Remove("Status");
            ModelState.Remove("Requests");

            if (product.ExpiryDate <= DateOnly.FromDateTime(DateTime.Today))
            {
                ModelState.AddModelError("ExpiryDate", "لا يمكن التبرع بدواء منتهي الصلاحية.");
            }

            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                    Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }

                    product.ProductImageUrl = "/images/products/" + uniqueFileName;
                }

                product.Status = "Pending";
                product.CreatedAt = DateTime.Now;

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم إرسال التبرع بنجاح وهو قيد المراجعة.";
                return RedirectToAction(nameof(Index));
            }

            var verifiedPharmacies = await _context.Pharmacies.Where(p => p.IsVerified).ToListAsync();
            ViewBag.Pharmacies = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(verifiedPharmacies, "PharmacyId", "PharmacyName", product.PharmacyId);
            ViewBag.Categories = new List<string> { "أدوية", "مستلزمات طبية", "فيتامينات", "أجهزة طبية" };

            return View(product);
        }
    }
}