using System.ComponentModel.DataAnnotations;

namespace Dawi.Models
    {
        public class User
        {
            [Key]
            public int UserID { get; set; }

            [Required(ErrorMessage = "الاسم مطلوب")]
            public string FullName { get; set; }

            [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
            [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
            public string Email { get; set; }

            [Required(ErrorMessage = "كلمة المرور مطلوبة")]
            public string PasswordHash { get; set; }

            public string? PhoneNumber { get; set; }

            [Required]
            public string Role { get; set; } = "Beneficiary"; // القيم الافتراضية: (Donor, Beneficiary, Pharmacist, Admin)

            [Required(ErrorMessage = "الرقم القومي مطلوب ")]
            [RegularExpression(@"^[23]\d{13}$", ErrorMessage = "الرقم القومي غير صحيح ")]
            public string NationalID { get; set; }

            public DateTime CreatedAt { get; set; } = DateTime.Now;
        }
    }


