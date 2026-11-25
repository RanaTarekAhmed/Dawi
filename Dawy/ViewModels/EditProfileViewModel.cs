

using System.ComponentModel.DataAnnotations;

namespace Dawi.ViewModels
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(150)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number.")]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }
    }
}

