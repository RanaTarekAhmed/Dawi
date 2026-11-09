namespace Dawy.Models
{
    public class Pharmacy
    {
        public int PharmacyId { get; set; }
        public string PharmacyName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public string City { get; set; } = string.Empty;

        public int? PharmacistId { get; set; }
        public User? Pharmacist { get; set; }

    }
}