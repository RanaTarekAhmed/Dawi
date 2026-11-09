using System.Security.Claims;
using Dawy.Data;
using Dawy.Models;
using Dawy.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
{
    public class AccountController : Controller
    {
        private readonly DawiContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AccountController(DawiContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!Roles.SelfAssignable.Contains(model.Role))
            {
                ModelState.AddModelError(nameof(model.Role), "Invalid account type.");
                return BadRequest(ModelState);
            }

            if (model.Role == Roles.Pharmacist)
            {
                if (string.IsNullOrWhiteSpace(model.PharmacyName))
                    ModelState.AddModelError(nameof(model.PharmacyName), "Pharmacy name is required.");

                if (string.IsNullOrWhiteSpace(model.PharmacyAddress))
                    ModelState.AddModelError(nameof(model.PharmacyAddress), "Pharmacy address is required.");

                if (string.IsNullOrWhiteSpace(model.PharmacyCity))
                    ModelState.AddModelError(nameof(model.PharmacyCity), "Pharmacy city is required.");

                if (string.IsNullOrWhiteSpace(model.PharmacyPhoneNumber))
                    ModelState.AddModelError(nameof(model.PharmacyPhoneNumber), "Pharmacy phone number is required.");

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);
            }

            var email = model.Email.Trim();
            var nationalId = model.NationalId.Trim();

            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                ModelState.AddModelError(nameof(model.Email), "This email address is already in use.");
                return BadRequest(ModelState);
            }

            if (await _context.Users.AnyAsync(u => u.NationalId == nationalId))
            {
                ModelState.AddModelError(nameof(model.NationalId), "This national ID is already registered.");
                return BadRequest(ModelState);
            }

            var user = new User
            {
                FullName = model.FullName.Trim(),
                Email = email,
                PhoneNumber = model.PhoneNumber?.Trim(),
                NationalId = nationalId,
                Role = model.Role,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

            if (model.Role == Roles.Pharmacist)
            {
                user.ManagedPharmacies.Add(new Pharmacy
                {
                    PharmacyName = model.PharmacyName!.Trim(),
                    Address = model.PharmacyAddress!.Trim(),
                    City = model.PharmacyCity!.Trim(),
                    PhoneNumber = model.PharmacyPhoneNumber!.Trim(),
                    IsVerified = false
                });
            }

            _context.Users.Add(user);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "Unable to create the account. The email or national ID may already be registered.");
                return BadRequest(ModelState);
            }

            await SignInUserAsync(user, isPersistent: false);

            return RedirectByRole(user.Role);
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            const string genericError = "Email or password is incorrect.";

            var email = model.Email.Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, genericError);
                return BadRequest(ModelState);
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, genericError);
                return BadRequest(ModelState);
            }

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
                await _context.SaveChangesAsync();
            }

            await SignInUserAsync(user, model.RememberMe);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectByRole(user.Role);
        }

        // POST: /Account/Logout
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // ---------------------- Helpers ----------------------

        private async Task SignInUserAsync(User user, bool isPersistent)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = isPersistent });
        }

        // Temporary: all roles go to Home until their controllers exist
        private IActionResult RedirectByRole(string role)
        {
            return RedirectToAction("Index", "Home");
        }
    }
}