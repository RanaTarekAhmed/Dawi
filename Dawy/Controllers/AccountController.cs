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

        // GET: /Account/Register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View(new RegisterViewModel { Role = Roles.Beneficiary });
        }

        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (!Roles.SelfAssignable.Contains(model.Role))
            {
                ModelState.AddModelError(nameof(model.Role), "نوع الحساب غير صالح.");
                return View(model);
            }

            if (model.Role == Roles.Pharmacist)
            {
                if (string.IsNullOrWhiteSpace(model.PharmacyName))
                    ModelState.AddModelError(nameof(model.PharmacyName), "اسم الصيدلية مطلوب.");

                if (string.IsNullOrWhiteSpace(model.PharmacyAddress))
                    ModelState.AddModelError(nameof(model.PharmacyAddress), "عنوان الصيدلية مطلوب.");

                if (string.IsNullOrWhiteSpace(model.PharmacyCity))
                    ModelState.AddModelError(nameof(model.PharmacyCity), "مدينة الصيدلية مطلوبة.");

                if (string.IsNullOrWhiteSpace(model.PharmacyPhoneNumber))
                    ModelState.AddModelError(nameof(model.PharmacyPhoneNumber), "رقم هاتف الصيدلية مطلوب.");

                if (!ModelState.IsValid)
                    return View(model);
            }

            var email = model.Email.Trim();
            var nationalId = model.NationalId.Trim();

            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                ModelState.AddModelError(nameof(model.Email), "البريد الإلكتروني مستخدم بالفعل.");
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.NationalId == nationalId))
            {
                ModelState.AddModelError(nameof(model.NationalId), "الرقم القومي مسجل بالفعل.");
                return View(model);
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
                ModelState.AddModelError(string.Empty, "تعذر إنشاء الحساب. قد يكون البريد الإلكتروني أو الرقم القومي مسجلاً بالفعل.");
                return View(model);
            }

            await SignInUserAsync(user, isPersistent: false);

            return RedirectByRole(user.Role);
        }

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            const string genericError = "البريد الإلكتروني أو كلمة المرور غير صحيحة.";

            var email = model.Email.Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, genericError);
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "هذا الحساب غير نشط.");
                return View(model);
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, genericError);
                return View(model);
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

        // GET: /Account/AccessDenied
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
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
