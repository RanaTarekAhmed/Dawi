using System.Security.Claims;
using Dawy.Data;
using Dawy.Models;
using Dawy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
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
        // Temporary: until role controllers exist, everyone goes to Home
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Home");
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
                return BadRequest(ModelState);

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