using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyBrandingAndUpi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "LogoImage",
                table: "CompanyDetails",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "QrCodeImage",
                table: "CompanyDetails",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SealImage",
                table: "CompanyDetails",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SignatureImage",
                table: "CompanyDetails",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpiId",
                table: "CompanyDetails",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoImage",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "QrCodeImage",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "SealImage",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "SignatureImage",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "UpiId",
                table: "CompanyDetails");
        }
    }
}
