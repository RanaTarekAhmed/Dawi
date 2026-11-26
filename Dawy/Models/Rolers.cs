
namespace Dawi.Models
{
    public static class Roles
    {
        public const string Donor = "Donor";
        public const string Beneficiary = "Beneficiary";
        public const string Pharmacist = "Pharmacist";
        public const string Admin = "Admin";

        public static readonly string[] SelfAssignable = { Donor, Beneficiary, Pharmacist };

        public static bool IsSelfAssignable(string? role) =>
            !string.IsNullOrWhiteSpace(role) && SelfAssignable.Contains(role.Trim());
    }
}

