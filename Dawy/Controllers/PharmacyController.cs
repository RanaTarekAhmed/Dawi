using System.Security.Claims;
using Dawi.Data;
using Dawi.Models;
using Dawi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawi.Controllers
{
    [Authorize]
    public class PharmacyController : Controller
    {
        private readonly DawiContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public PharmacyController(DawiContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // GET: /Pharmacy
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? city, string? search)
        {
            var query = _context.Pharmacies
                .AsNoTracking()
                .Include(p => p.Pharmacist)
                .Where(p => p.IsVerified)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(city) && city != "الكل")
            {
                var trimmedCity = city.Trim();
                query = query.Where(p => p.City == trimmedCity);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmedSearch = search.Trim();
                query = query.Where(p => p.PharmacyName.Contains(trimmedSearch) || p.Address.Contains(trimmedSearch));
            }

            var pharmacies = await query
                .OrderBy(p => p.City)
                .ThenBy(p => p.PharmacyName)
                .ToListAsync();

            var cities = await _context.Pharmacies
                .AsNoTracking()
                .Where(p => p.IsVerified && !string.IsNullOrEmpty(p.City))
                .Select(p => p.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.Cities = cities;
            ViewBag.CurrentCity = city;
            ViewBag.CurrentSearch = search;

            return View(pharmacies);
        }

        // GET: /Pharmacy/Details/5
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var pharmacy = await _context.Pharmacies
                .AsNoTracking()
                .Include(p => p.Pharmacist)
                .FirstOrDefaultAsync(p => p.PharmacyId == id);

            if (pharmacy == null)
                return NotFound();

            if (!pharmacy.IsVerified)
            {
                bool isOwnerOrAdmin = User.Identity?.IsAuthenticated == true &&
                    (User.IsInRole(Roles.Admin) || (User.IsInRole(Roles.Pharmacist) && pharmacy.PharmacistId == CurrentUserId));

                if (!isOwnerOrAdmin)
                    return NotFound();
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            var availableProducts = await _context.Products
                .AsNoTracking()
                .Where(p => p.PharmacyId == pharmacy.PharmacyId
                         && p.Status == ProductStatus.Approved
                         && p.Quantity > 0
                         && p.ExpiryDate >= today)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.AvailableProducts = availableProducts;
            return View(pharmacy);
        }

        // GET: /Pharmacy/MyPharmacies
        [HttpGet]
        [Authorize(Roles = Roles.Pharmacist)]
        public async Task<IActionResult> MyPharmacies()
        {
            var myPharmacies = await _context.Pharmacies
                .AsNoTracking()
                .Where(p => p.PharmacistId == CurrentUserId)
                .OrderByDescending(p => p.IsVerified)
                .ThenBy(p => p.PharmacyName)
                .ToListAsync();

            var pharmacyIds = myPharmacies.Select(p => p.PharmacyId).ToList();

            ViewBag.PendingInspectionsCount = await _context.Products
                .CountAsync(p => p.PharmacyId.HasValue && pharmacyIds.Contains(p.PharmacyId.Value) && p.Status == ProductStatus.Pending);

            ViewBag.ApprovedProductsCount = await _context.Products
                .CountAsync(p => p.PharmacyId.HasValue && pharmacyIds.Contains(p.PharmacyId.Value) && p.Status == ProductStatus.Approved && p.Quantity > 0);

            ViewBag.ActiveRequestsCount = await _context.Requests
                .Include(r => r.Product)
                .CountAsync(r => r.Product.PharmacyId.HasValue && pharmacyIds.Contains(r.Product.PharmacyId.Value) && r.CollectionDate == null && r.RejectionReason == null);

            return View(myPharmacies);
        }

        // GET: /Pharmacy/Create
        [HttpGet]
        [Authorize(Roles = Roles.Pharmacist)]
        public IActionResult Create()
        {
            return View(new Pharmacy());
        }

        // POST: /Pharmacy/Create
        [HttpPost]
        [Authorize(Roles = Roles.Pharmacist)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Pharmacy model, IFormFile? licenseDocument, IFormFile? identityDocument)
        {
            ModelState.Remove(nameof(Pharmacy.Pharmacist));
            ModelState.Remove(nameof(Pharmacy.IsVerified));

            if (string.IsNullOrWhiteSpace(model.PharmacyName))
                ModelState.AddModelError(nameof(model.PharmacyName), "اسم الصيدلية مطلوب.");

            if (string.IsNullOrWhiteSpace(model.Address))
                ModelState.AddModelError(nameof(model.Address), "عنوان الصيدلية مطلوب.");

            if (string.IsNullOrWhiteSpace(model.City))
                ModelState.AddModelError(nameof(model.City), "المدينة مطلوبة.");

            if (string.IsNullOrWhiteSpace(model.PhoneNumber))
                ModelState.AddModelError(nameof(model.PhoneNumber), "رقم الهاتف مطلوب.");

            if (licenseDocument == null || licenseDocument.Length == 0)
                ModelState.AddModelError("licenseDocument", "يرجى رفع رخصة مزاولة المهنة.");

            if (identityDocument == null || identityDocument.Length == 0)
                ModelState.AddModelError("identityDocument", "يرجى رفع صورة البطاقة أو السجل التجاري.");

            if (!ModelState.IsValid)
                return View(model);

            var (licenseUrl, licenseError) = await DocumentUploader.SaveAsync(
                licenseDocument, _webHostEnvironment, "pharmacy-documents", "رخصة الصيدلية");
            if (licenseError != null)
            {
                ModelState.AddModelError("licenseDocument", licenseError);
                return View(model);
            }

            var (identityUrl, identityError) = await DocumentUploader.SaveAsync(
                identityDocument, _webHostEnvironment, "pharmacy-documents", "صورة البطاقة أو السجل التجاري");
            if (identityError != null)
            {
                ModelState.AddModelError("identityDocument", identityError);
                return View(model);
            }

            var pharmacy = new Pharmacy
            {
                PharmacyName = model.PharmacyName.Trim(),
                Address = model.Address.Trim(),
                City = model.City.Trim(),
                PhoneNumber = model.PhoneNumber.Trim(),
                PharmacistId = CurrentUserId,
                IsVerified = false,
                RegistrationStatus = PharmacyRegistrationStatus.Pending,
                LicenseDocumentUrl = licenseUrl,
                IdentityDocumentUrl = identityUrl
            };

            _context.Pharmacies.Add(pharmacy);
            await _context.SaveChangesAsync();

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = CurrentUserId,
                Action = "PharmacyCreated",
                EntityName = nameof(Pharmacy),
                EntityId = pharmacy.PharmacyId.ToString(),
                Details = $"Pharmacy '{pharmacy.PharmacyName}' in '{pharmacy.City}' submitted for verification.",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            await NotifyAdminsAsync(
                "صيدلية جديدة بانتظار المراجعة",
                $"تم تسجيل الصيدلية '{pharmacy.PharmacyName}' ورفع أوراقها. يرجى المراجعة قبل الاعتماد.",
                "/Admin/Pharmacies");
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إرسال بيانات الصيدلية وأوراقها بنجاح وهي الآن قيد مراجعة الإدارة.";
            return RedirectToAction(nameof(MyPharmacies));
        }

        // GET: /Pharmacy/Edit/5
        [HttpGet]
        [Authorize(Roles = Roles.Pharmacist + "," + Roles.Admin)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var pharmacy = await _context.Pharmacies.FindAsync(id);
            if (pharmacy == null)
                return NotFound();

            if (User.IsInRole(Roles.Pharmacist) && pharmacy.PharmacistId != CurrentUserId)
                return Forbid();

            return View(pharmacy);
        }

        // POST: /Pharmacy/Edit/5
        [HttpPost]
        [Authorize(Roles = Roles.Pharmacist + "," + Roles.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Pharmacy model, IFormFile? licenseDocument, IFormFile? identityDocument)
        {
            var pharmacy = await _context.Pharmacies.FindAsync(id);
            if (pharmacy == null)
                return NotFound();

            if (User.IsInRole(Roles.Pharmacist) && pharmacy.PharmacistId != CurrentUserId)
                return Forbid();

            if (string.IsNullOrWhiteSpace(model.PharmacyName) ||
                string.IsNullOrWhiteSpace(model.Address) ||
                string.IsNullOrWhiteSpace(model.City) ||
                string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                ModelState.AddModelError(string.Empty, "جميع حقول الصيدلية الأساسية مطلوبة.");
                return View(model);
            }

            pharmacy.PharmacyName = model.PharmacyName.Trim();
            pharmacy.Address = model.Address.Trim();
            pharmacy.City = model.City.Trim();
            pharmacy.PhoneNumber = model.PhoneNumber.Trim();

            if (licenseDocument?.Length > 0 || identityDocument?.Length > 0)
            {
                if (licenseDocument?.Length > 0)
                {
                    var (licenseUrl, licenseError) = await DocumentUploader.SaveAsync(
                        licenseDocument, _webHostEnvironment, "pharmacy-documents", "رخصة الصيدلية");
                    if (licenseError != null)
                    {
                        ModelState.AddModelError("licenseDocument", licenseError);
                        return View(model);
                    }
                    pharmacy.LicenseDocumentUrl = licenseUrl;
                }

                if (identityDocument?.Length > 0)
                {
                    var (identityUrl, identityError) = await DocumentUploader.SaveAsync(
                        identityDocument, _webHostEnvironment, "pharmacy-documents", "صورة البطاقة أو السجل التجاري");
                    if (identityError != null)
                    {
                        ModelState.AddModelError("identityDocument", identityError);
                        return View(model);
                    }
                    pharmacy.IdentityDocumentUrl = identityUrl;
                }

                if (!pharmacy.IsVerified)
                {
                    pharmacy.RegistrationStatus = PharmacyRegistrationStatus.Pending;
                    pharmacy.RegistrationRejectionReason = null;
                    await NotifyAdminsAsync(
                        "تحديث أوراق صيدلية",
                        $"تم تحديث أوراق الصيدلية '{pharmacy.PharmacyName}' وهي بانتظار المراجعة.",
                        "/Admin/Pharmacies");
                }
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = CurrentUserId,
                Action = "PharmacyUpdated",
                EntityName = nameof(Pharmacy),
                EntityId = pharmacy.PharmacyId.ToString(),
                Details = $"Updated details for {pharmacy.PharmacyName}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تحديث بيانات الصيدلية بنجاح.";
            return User.IsInRole(Roles.Admin)
                ? RedirectToAction(nameof(Index))
                : RedirectToAction(nameof(MyPharmacies));
        }

        private async Task NotifyAdminsAsync(string title, string message, string? link)
        {
            var adminIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == Roles.Admin && u.IsActive)
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var adminId in adminIds)
                await NotifyAsync(adminId, title, message, link);
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
    }
}


