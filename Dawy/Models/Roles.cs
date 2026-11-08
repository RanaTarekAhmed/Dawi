
namespace Dawy.Models
{
    public static class Roles
    {
        public const string Donor = "Donor";
        public const string Beneficiary = "Beneficiary";
        public const string Pharmacist = "Pharmacist";
        public const string Admin = "Admin";

        public static readonly string[] SelfAssignable = { Donor, Beneficiary, Pharmacist };
    }
}