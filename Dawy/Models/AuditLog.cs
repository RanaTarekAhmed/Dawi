namespace Dawy.Models
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
