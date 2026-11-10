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
            return Ok(new
            {
                users = await _context.Users.CountAsync(),
                pendingProducts = await _context.Products.CountAsync(p => p.Status == ProductStatus.Pending),
                unverifiedPharmacies = await _context.Pharmacies.CountAsync(p => !p.IsVerified),
                unreadNotifications = await _context.Notifications.CountAsync(n => !n.IsRead)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Users(string? role, bool includeInactive = true)
        {
            var query = _context.Users.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(role)) query = query.Where(u => u.Role == role);
            if (!includeInactive) query = query.Where(u => u.IsActive);
            return Ok(await query.OrderBy(u => u.FullName)
                .Select(u => new { u.UserId, u.FullName, u.Email, u.Role, u.IsActive, u.CreatedAt })
                .ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetUserStatus(int id, bool isActive)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            if (user.UserId == CurrentUserId && !isActive) return BadRequest("You cannot deactivate your own account.");

            user.IsActive = isActive;
            await WriteAuditAsync("UserStatusChanged", nameof(User), user.UserId, $"IsActive={isActive}");
            await _context.SaveChangesAsync();
            return Ok(new { user.UserId, user.IsActive });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetUserRole(int id, string role)
        {
            if (!new[] { Roles.Donor, Roles.Beneficiary, Roles.Pharmacist, Roles.Admin }.Contains(role))
                return BadRequest("Invalid role.");
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            var oldRole = user.Role;
            user.Role = role;
            await WriteAuditAsync("UserRoleChanged", nameof(User), user.UserId, $"{oldRole} -> {role}");
            await _context.SaveChangesAsync();
            return Ok(new { user.UserId, user.Role });
        }

        [HttpGet]
        public async Task<IActionResult> Pharmacies(bool? verified = null)
        {
            var query = _context.Pharmacies.AsNoTracking().Include(p => p.Pharmacist).AsQueryable();
            if (verified.HasValue) query = query.Where(p => p.IsVerified == verified.Value);
            return Ok(await query.OrderBy(p => p.PharmacyName).Select(p => new
            {
                p.PharmacyId, p.PharmacyName, p.City, p.Address, p.PhoneNumber, p.IsVerified,
                Pharmacist = p.Pharmacist == null ? null : p.Pharmacist.FullName
            }).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPharmacy(int id, bool verified = true)
        {
            var pharmacy = await _context.Pharmacies.FindAsync(id);
            if (pharmacy == null) return NotFound();
            pharmacy.IsVerified = verified;
            await WriteAuditAsync("PharmacyVerificationChanged", nameof(Pharmacy), id, $"IsVerified={verified}");
            if (pharmacy.PharmacistId.HasValue)
                await NotifyAsync(pharmacy.PharmacistId.Value, "Pharmacy verification", verified ? "Your pharmacy has been verified." : "Your pharmacy verification was revoked.", "/Profile");
            await _context.SaveChangesAsync();
            return Ok(new { pharmacy.PharmacyId, pharmacy.IsVerified });
        }

        [HttpGet]
        public async Task<IActionResult> Products(string? status = null)
        {
            var query = _context.Products.AsNoTracking().Include(p => p.Donor).Include(p => p.Pharmacy).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);
            return Ok(await query.OrderByDescending(p => p.CreatedAt).Select(p => new
            {
                p.ProductId, p.ProductName, p.Category, p.Quantity, p.ExpiryDate, p.Status,
                Donor = p.Donor.FullName, Pharmacy = p.Pharmacy == null ? null : p.Pharmacy.PharmacyName
            }).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ModerateProduct(int id, string status, string? reason = null)
        {
            if (status != ProductStatus.Approved && status != ProductStatus.Rejected)
                return BadRequest("Status must be Approved or Rejected.");
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            product.Status = status;
            await WriteAuditAsync("ProductModerated", nameof(Product), id, reason ?? status);
            await NotifyAsync(product.DonorId, "Donation status updated", $"Your donation '{product.ProductName}' is {status.ToLowerInvariant()}.", "/Product/MyDonations");
            await _context.SaveChangesAsync();
            return Ok(new { product.ProductId, product.Status });
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
                UserId = CurrentUserId, Action = action, EntityName = entityName,
                EntityId = entityId.ToString(), Details = details, CreatedAt = DateTime.UtcNow
            }).AsTask();
    }
}
