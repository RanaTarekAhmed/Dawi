using System.ComponentModel.DataAnnotations;

namespace Dawy.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(150)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [StringLength(200)]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number.")]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "National ID is required.")]
        [RegularExpression(@"^\d{14}$", ErrorMessage = "The national ID must be 14 digits.")]
        [Display(Name = "National ID")]
        public string NationalId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Account type is required.")]
        [Display(Name = "Account Type")]
        public string Role { get; set; } = string.Empty;

        [StringLength(150)]
        [Display(Name = "Pharmacy Name")]
        public string? PharmacyName { get; set; }

        [StringLength(300)]
        [Display(Name = "Pharmacy Address")]
        public string? PharmacyAddress { get; set; }

        [StringLength(100)]
        [Display(Name = "Pharmacy City")]
        public string? PharmacyCity { get; set; }

        [Phone(ErrorMessage = "Invalid pharmacy phone number.")]
        [StringLength(20)]
        [Display(Name = "Pharmacy Phone")]
        public string? PharmacyPhoneNumber { get; set; }
    }
}