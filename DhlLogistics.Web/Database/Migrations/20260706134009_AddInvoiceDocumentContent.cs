using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceDocumentContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Content",
                table: "InvoiceDocuments",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                table: "InvoiceDocuments");
        }
    }
}
