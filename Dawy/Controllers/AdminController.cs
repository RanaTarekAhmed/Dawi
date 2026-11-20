using System.Security.Claims;
using Dawy.Data;
using Dawy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class AdminController : Controller
    {
        private readonly DawiContext _context;

        public AdminController(DawiContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.VerifiedPharmacies = await _context.Pharmacies.CountAsync(p => p.IsVerified);
            ViewBag.UnverifiedPharmacies = await _context.Pharmacies.CountAsync(p => !p.IsVerified);

            ViewBag.PendingProducts = await _context.Products.CountAsync(p => p.Status == ProductStatus.Pending);
            ViewBag.ApprovedProducts = await _context.Products.CountAsync(p => p.Status == ProductStatus.Approved);
            ViewBag.RejectedProducts = await _context.Products.CountAsync(p => p.Status == ProductStatus.Rejected);

            ViewBag.TotalRequests = await _context.Requests.CountAsync();
            ViewBag.UnreadNotifications = await _context.Notifications.CountAsync(n => !n.IsRead);

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Users(string? role, string? search, bool includeInactive = true)
        {
            var query = _context.Users.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(role)) query = query.Where(u => u.Role == role);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(u => u.FullName.Contains(term) || u.Email.Contains(term));
            }



            if (!includeInactive) query = query.Where(u => u.IsActive);
            var users = await query
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.Role = role;
            ViewBag.Search = search;
            ViewBag.IncludeInactive = includeInactive;

            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetUserStatus(int id, bool isActive)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }



            if (user.UserId == CurrentUserId && !isActive)
            {
                TempData["Error"] = "You cannot deactivate your own account.";
                return RedirectToAction(nameof(Users));
            }

            user.IsActive = isActive;
            await WriteAuditAsync("UserStatusChanged", nameof(User), user.UserId, $"IsActive={isActive}");
            await _context.SaveChangesAsync();

            TempData["Success"] = isActive ? "User account activated." : "User account deactivated.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetUserRole(int id, string role)
        {
            if (!new[] { Roles.Donor, Roles.Beneficiary, Roles.Pharmacist, Roles.Admin }.Contains(role))
            {
                TempData["Error"] = "Invalid role.";
                return RedirectToAction(nameof(Users));
            }
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }
            var oldRole = user.Role;
            user.Role = role;
            await WriteAuditAsync("UserRoleChanged", nameof(User), user.UserId, $"{oldRole} -> {role}");
            await _context.SaveChangesAsync();
            TempData["Success"] = $"User role updated from {oldRole} to {role}.";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> Pharmacies(bool? verified = null, string? search = null)
        {
            var query = _context.Pharmacies.AsNoTracking().Include(p => p.Pharmacist).AsQueryable();
            if (verified.HasValue) query = query.Where(p => p.IsVerified == verified.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p =>
                    p.PharmacyName.Contains(term) ||
                    p.City.Contains(term)) ;

                   
            }

            var pharmacies = await query
                .OrderBy(p => p.PharmacyName)
                .ToListAsync();

            ViewBag.Verified = verified;
            ViewBag.Search = search;

            return View(pharmacies);
        }
        

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPharmacy(int id, bool verified = true)
        {
            var pharmacy = await _context.Pharmacies.FindAsync(id);
            if (pharmacy == null)
            {
                TempData["Error"] = "Pharmacy not found.";
                return RedirectToAction(nameof(Pharmacies));
            }
            pharmacy.IsVerified = verified;
            await WriteAuditAsync("PharmacyVerificationChanged", nameof(Pharmacy), id, $"IsVerified={verified}");
            if (pharmacy.PharmacistId.HasValue)
                await NotifyAsync(pharmacy.PharmacistId.Value, "Pharmacy verification", verified ? "Your pharmacy has been verified." : "Your pharmacy verification was revoked.", "/Profile");
            await _context.SaveChangesAsync();
            TempData["Success"] = verified ? "Pharmacy verified successfully." : "Pharmacy verification revoked.";
            return RedirectToAction(nameof(Pharmacies)); 
        }

            [HttpGet]
        public async Task<IActionResult> Products(string? status = null, string? search = null)
        {
            var query = _context.Products.AsNoTracking().Include(p => p.Donor).Include(p => p.Pharmacy).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p => p.ProductName.Contains(term) || p.Category.Contains(term));
            }

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.Status = status;
            ViewBag.Search = search;

            return View(products);
        
    }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SetProductStatus(int id, string status, string? reason = null) =>
            ModerateProduct(id, status, reason);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ModerateProduct(int id, string status, string? reason = null)
        {
            if (status != ProductStatus.Approved && status != ProductStatus.Rejected)
            {
                TempData["Error"] = "Status must be Approved or Rejected.";
                return RedirectToAction(nameof(Products));
            }
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction(nameof(Products));
            }
            var cleanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            product.Status = status;
            if (status == ProductStatus.Rejected)
                product.RejectionReason = cleanReason;

            await WriteAuditAsync("ProductModerated", nameof(Product), id, cleanReason ?? status);
            await NotifyAsync(
                product.DonorId,
                "Donation status updated",
                $"Your donation '{product.ProductName}' is {status.ToLowerInvariant()}.",
                "/Product/MyDonations");

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Product status updated to {status}.";
            return RedirectToAction(nameof(Products));
        
    }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private Task NotifyAsync(int userId, string title, string message, string? link) =>
            _context.Notifications.AddAsync(new Notification
            {
                UserId = userId, Title = title, Message = message, Link = link,
                CreatedAt = DateTime.UtcNow
            }).AsTask();

        private Task WriteAuditAsync(string action, string entityName, int entityId, string? details) =>
            _context.AuditLogs.AddAsync(new AuditLog
            {
                UserId = CurrentUserId, 
                Action = action, 
                EntityName = entityName,
                EntityId = entityId.ToString(), 
                Details = details,
                CreatedAt = DateTime.UtcNow
            }).AsTask();
    }
}

