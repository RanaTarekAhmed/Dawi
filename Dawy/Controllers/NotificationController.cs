using System.Security.Claims;
using Dawy.Data;
using Dawy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
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
            return Ok(await query.OrderByDescending(n => n.CreatedAt).ToListAsync());
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
            return Ok(new { notification.NotificationId, notification.IsRead });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            await _context.Notifications.Where(n => n.UserId == CurrentUserId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
            return Ok();
        }
    }
}
