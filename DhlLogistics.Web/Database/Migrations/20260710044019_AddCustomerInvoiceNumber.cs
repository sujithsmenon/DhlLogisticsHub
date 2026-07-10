using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerInvoiceNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerInvoiceNumber",
                table: "JobOrders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerInvoiceNumber",
                table: "Bills",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_CustomerInvoiceNumber",
                table: "JobOrders",
                column: "CustomerInvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_CustomerInvoiceNumber",
                table: "Bills",
                column: "CustomerInvoiceNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobOrders_CustomerInvoiceNumber",
                table: "JobOrders");

            migrationBuilder.DropIndex(
                name: "IX_Bills_CustomerInvoiceNumber",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CustomerInvoiceNumber",
                table: "JobOrders");

            migrationBuilder.DropColumn(
                name: "CustomerInvoiceNumber",
                table: "Bills");
        }
    }
}
