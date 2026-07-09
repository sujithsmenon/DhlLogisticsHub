using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationChargeLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "JobOperationId",
                table: "JobCharges",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "JobOperationId",
                table: "BillCharges",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationName",
                table: "BillCharges",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobCharges_JobOperationId",
                table: "JobCharges",
                column: "JobOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_BillCharges_JobOperationId",
                table: "BillCharges",
                column: "JobOperationId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobCharges_JobOperations_JobOperationId",
                table: "JobCharges",
                column: "JobOperationId",
                principalTable: "JobOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobCharges_JobOperations_JobOperationId",
                table: "JobCharges");

            migrationBuilder.DropIndex(
                name: "IX_JobCharges_JobOperationId",
                table: "JobCharges");

            migrationBuilder.DropIndex(
                name: "IX_BillCharges_JobOperationId",
                table: "BillCharges");

            migrationBuilder.DropColumn(
                name: "JobOperationId",
                table: "JobCharges");

            migrationBuilder.DropColumn(
                name: "JobOperationId",
                table: "BillCharges");

            migrationBuilder.DropColumn(
                name: "OperationName",
                table: "BillCharges");
        }
    }
}
