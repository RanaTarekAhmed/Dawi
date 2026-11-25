using Dawi.Data;
using Dawi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dawi.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class AuditController : Controller
    {
        private readonly DawiContext _context;

        public AuditController(DawiContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? entityName)
        {
            var query = _context.AuditLogs.AsNoTracking().Include(a => a.User).AsQueryable();

            if (!string.IsNullOrWhiteSpace(entityName))
            {
                query = query.Where(a => a.EntityName == entityName);
            }

            var logs = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
            return View(logs);
        }
    }
}

