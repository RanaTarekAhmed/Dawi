namespace Dawi.Models
{
    public class Pharmacy
    {
        public int PharmacyId { get; set; }
        public string PharmacyName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public string City { get; set; } = string.Empty;

        // مراجعة الإدارة لأوراق التقديم قبل الاعتماد
        public string RegistrationStatus { get; set; } = PharmacyRegistrationStatus.Pending;
        public string? RegistrationRejectionReason { get; set; }
        public string? LicenseDocumentUrl { get; set; }
        public string? IdentityDocumentUrl { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByAdminId { get; set; }

        public int? PharmacistId { get; set; }
        public User? Pharmacist { get; set; }

    }
}

