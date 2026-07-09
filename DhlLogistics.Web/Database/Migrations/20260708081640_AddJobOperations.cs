using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddJobOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobOperations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobOrderId = table.Column<long>(type: "bigint", nullable: false),
                    SequenceNo = table.Column<int>(type: "integer", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PlannedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedStaffId = table.Column<int>(type: "integer", nullable: true),
                    VendorId = table.Column<int>(type: "integer", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobOperations_Clients_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobOperations_JobOrders_JobOrderId",
                        column: x => x.JobOrderId,
                        principalTable: "JobOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobOperations_Staff_AssignedStaffId",
                        column: x => x.AssignedStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobOperations_AssignedStaffId",
                table: "JobOperations",
                column: "AssignedStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOperations_JobOrderId",
                table: "JobOperations",
                column: "JobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOperations_JobOrderId_SequenceNo",
                table: "JobOperations",
                columns: new[] { "JobOrderId", "SequenceNo" });

            migrationBuilder.CreateIndex(
                name: "IX_JobOperations_Status",
                table: "JobOperations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_JobOperations_VendorId",
                table: "JobOperations",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobOperations");
        }
    }
}
