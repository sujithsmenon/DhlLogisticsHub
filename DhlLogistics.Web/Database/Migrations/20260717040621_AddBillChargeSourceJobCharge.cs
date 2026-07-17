using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBillChargeSourceJobCharge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourceJobChargeId",
                table: "BillCharges",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillCharges_SourceJobChargeId",
                table: "BillCharges",
                column: "SourceJobChargeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BillCharges_SourceJobChargeId",
                table: "BillCharges");

            migrationBuilder.DropColumn(
                name: "SourceJobChargeId",
                table: "BillCharges");
        }
    }
}
