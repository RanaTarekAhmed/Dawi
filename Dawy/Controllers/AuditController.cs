using Dawy.Data;
using Dawy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawy.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class AuditController : Controller
    {
        private readonly DawiContext _context;
        public AuditController(DawiContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Index(string? actionName, string? entityName, DateTime? from, DateTime? to, int page = 1, int pageSize = 100)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 500);
            var query = _context.AuditLogs.AsNoTracking().Include(a => a.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(actionName)) query = query.Where(a => a.Action == actionName);
            if (!string.IsNullOrWhiteSpace(entityName)) query = query.Where(a => a.EntityName == entityName);
            if (from.HasValue) query = query.Where(a => a.CreatedAt >= from.Value.ToUniversalTime());
            if (to.HasValue) query = query.Where(a => a.CreatedAt < to.Value.ToUniversalTime().AddDays(1));
            var total = await query.CountAsync();
            var logs = await query.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new { a.AuditLogId, a.Action, a.EntityName, a.EntityId, a.Details, a.CreatedAt, User = a.User == null ? null : a.User.FullName })
                .ToListAsync();
            return Ok(new { page, pageSize, total, items = logs });
        }
    }
}
