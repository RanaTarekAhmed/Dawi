using System.Security.Claims;
using Dawi.Data;
using Dawi.Models;
using Dawi.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawi.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly DawiContext _context;

        public ProfileController(DawiContext context)
        {
            _context = context;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // GET: /Profile
        public async Task<IActionResult> Index()
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == CurrentUserId);

            if (user == null)
                return Challenge();

            return View(user);
        }

        // GET: /Profile/Edit
        public async Task<IActionResult> Edit()
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
            if (user == null)
                return Challenge();

            return View(new EditProfileViewModel
            {
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber
            });
        }

        // POST: /Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
            if (user == null)
                return Challenge();

            user.FullName = model.FullName.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim();

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Profile updated successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}


