using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerInvoiceNumberToAwbAndExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerInvoiceNumber",
                table: "ExportJobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerInvoiceNumber",
                table: "AwbShipments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_CustomerInvoiceNumber",
                table: "ExportJobs",
                column: "CustomerInvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AwbShipments_CustomerInvoiceNumber",
                table: "AwbShipments",
                column: "CustomerInvoiceNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExportJobs_CustomerInvoiceNumber",
                table: "ExportJobs");

            migrationBuilder.DropIndex(
                name: "IX_AwbShipments_CustomerInvoiceNumber",
                table: "AwbShipments");

            migrationBuilder.DropColumn(
                name: "CustomerInvoiceNumber",
                table: "ExportJobs");

            migrationBuilder.DropColumn(
                name: "CustomerInvoiceNumber",
                table: "AwbShipments");
        }
    }
}
