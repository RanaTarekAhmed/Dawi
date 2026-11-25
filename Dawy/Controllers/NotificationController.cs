using System.Security.Claims;
using Dawi.Data;
using Dawi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawi.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly DawiContext _context;
        public NotificationController(DawiContext context) => _context = context;
        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> Index(bool unreadOnly = false)
        {
            var query = _context.Notifications.AsNoTracking().Where(n => n.UserId == CurrentUserId);
            if (unreadOnly) query = query.Where(n => !n.IsRead);
           
            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(items);
        }

        [HttpGet]
        public Task<int> UnreadCount() => _context.Notifications.CountAsync(n => n.UserId == CurrentUserId && !n.IsRead);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == CurrentUserId);
            if (notification == null) return NotFound();
            notification.IsRead = true;
            await _context.SaveChangesAsync();
            
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            await _context.Notifications.Where(n => n.UserId == CurrentUserId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
   
            return RedirectToAction(nameof(Index));
        }
    }
}

