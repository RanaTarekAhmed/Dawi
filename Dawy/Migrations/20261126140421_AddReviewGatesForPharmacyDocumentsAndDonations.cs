using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dawy.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewGatesForPharmacyDocumentsAndDonations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdentityDocumentUrl",
                table: "Pharmacies",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseDocumentUrl",
                table: "Pharmacies",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationRejectionReason",
                table: "Pharmacies",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationStatus",
                table: "Pharmacies",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Pharmacies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByAdminId",
                table: "Pharmacies",
                type: "int",
                nullable: true);

            // الصيدليات المعتمدة قبل إضافة المراجعة تعتبر أوراقها مقبولة بالفعل
            migrationBuilder.Sql("UPDATE [Pharmacies] SET [RegistrationStatus] = N'Approved' WHERE [IsVerified] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdentityDocumentUrl",
                table: "Pharmacies");

            migrationBuilder.DropColumn(
                name: "LicenseDocumentUrl",
                table: "Pharmacies");

            migrationBuilder.DropColumn(
                name: "RegistrationRejectionReason",
                table: "Pharmacies");

            migrationBuilder.DropColumn(
                name: "RegistrationStatus",
                table: "Pharmacies");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Pharmacies");

            migrationBuilder.DropColumn(
                name: "ReviewedByAdminId",
                table: "Pharmacies");
        }
    }
}
