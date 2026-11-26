using System.Security.Claims;
using Dawi.Data;
using Dawi.Models;
using Dawi.Services;
using Dawi.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawi.Controllers
{
    public class AccountController : Controller
    {
        private readonly DawiContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AccountController(DawiContext context, IPasswordHasher<User> passwordHasher, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _webHostEnvironment = webHostEnvironment;
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
            var requstedRole = model.Role?.Trim() ?? string.Empty;
            if (string.Equals(requstedRole,Roles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.Role), "لا يمكن إنشاء حساب مدير من صفحة التسجيل.");
                return View(model);
            }
            if (!Roles.IsSelfAssignable(requstedRole))
            {
                ModelState.AddModelError(nameof(model.Role), "نوع الحساب غير صالح.");
                return View(model);
            }


            if (requstedRole == Roles.Pharmacist)
            {
                if (string.IsNullOrWhiteSpace(model.PharmacyName))
                    ModelState.AddModelError(nameof(model.PharmacyName), "اسم الصيدلية مطلوب.");

                if (string.IsNullOrWhiteSpace(model.PharmacyAddress))
                    ModelState.AddModelError(nameof(model.PharmacyAddress), "عنوان الصيدلية مطلوب.");

                if (string.IsNullOrWhiteSpace(model.PharmacyCity))
                    ModelState.AddModelError(nameof(model.PharmacyCity), "مدينة الصيدلية مطلوبة.");

                if (string.IsNullOrWhiteSpace(model.PharmacyPhoneNumber))
                    ModelState.AddModelError(nameof(model.PharmacyPhoneNumber), "رقم هاتف الصيدلية مطلوب.");

                if (model.PharmacyLicenseFile == null || model.PharmacyLicenseFile.Length == 0)
                    ModelState.AddModelError(nameof(model.PharmacyLicenseFile), "يرجى رفع رخصة مزاولة المهنة.");

                if (model.PharmacyIdentityFile == null || model.PharmacyIdentityFile.Length == 0)
                    ModelState.AddModelError(nameof(model.PharmacyIdentityFile), "يرجى رفع صورة البطاقة أو السجل التجاري.");

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

            string? licenseUrl = null;
            string? identityUrl = null;
            if (requstedRole == Roles.Pharmacist)
            {
                var (licenseResult, licenseError) = await DocumentUploader.SaveAsync(
                    model.PharmacyLicenseFile, _webHostEnvironment, "pharmacy-documents", "رخصة الصيدلية");
                if (licenseError != null)
                    ModelState.AddModelError(nameof(model.PharmacyLicenseFile), licenseError);
                licenseUrl = licenseResult;

                var (identityResult, identityError) = await DocumentUploader.SaveAsync(
                    model.PharmacyIdentityFile, _webHostEnvironment, "pharmacy-documents", "صورة البطاقة أو السجل التجاري");
                if (identityError != null)
                    ModelState.AddModelError(nameof(model.PharmacyIdentityFile), identityError);
                identityUrl = identityResult;

                if (!ModelState.IsValid)
                    return View(model);
            }

            var user = new User
            {
                FullName = model.FullName.Trim(),
                Email = email,
                PhoneNumber = model.PhoneNumber?.Trim(),
                NationalId = nationalId,
                Role = requstedRole,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

            if (requstedRole == Roles.Pharmacist)
            {
                user.ManagedPharmacies.Add(new Pharmacy
                {
                    PharmacyName = model.PharmacyName!.Trim(),
                    Address = model.PharmacyAddress!.Trim(),
                    City = model.PharmacyCity!.Trim(),
                    PhoneNumber = model.PharmacyPhoneNumber!.Trim(),
                    IsVerified = false,
                    RegistrationStatus = PharmacyRegistrationStatus.Pending,
                    LicenseDocumentUrl = licenseUrl,
                    IdentityDocumentUrl = identityUrl
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

            if (requstedRole == Roles.Pharmacist)
            {
                await NotifyAdminsAsync(
                    "صيدلية جديدة بانتظار المراجعة",
                    $"تم تسجيل الصيدلية '{model.PharmacyName!.Trim()}' ورفع أوراقها. يرجى المراجعة قبل الاعتماد.",
                    "/Admin/Pharmacies");
                await _context.SaveChangesAsync();
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

        private async Task NotifyAdminsAsync(string title, string message, string? link)
        {
            var adminIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == Roles.Admin && u.IsActive)
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var adminId in adminIds)
            {
                await NotifyAsync(adminId, title, message, link);
            }
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

        private IActionResult RedirectByRole(string role)
        {
            return role switch
            {
                Roles.Donor => RedirectToAction("Index", "Donation"),
                Roles.Pharmacist => RedirectToAction("MyPharmacies", "Pharmacy"),
                Roles.Admin => RedirectToAction("Index", "Admin"),
                Roles.Beneficiary => RedirectToAction("Index", "Category"),
                _ => RedirectToAction("Index", "Home")
            };
        }
    }
}


