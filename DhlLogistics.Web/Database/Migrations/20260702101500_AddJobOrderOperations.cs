using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddJobOrderOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobOrderOperations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobOrderId = table.Column<long>(type: "bigint", nullable: false),
                    OperationName = table.Column<string>(type: "text", nullable: false),
                    OperatedByClientId = table.Column<int>(type: "integer", nullable: true),
                    HandledByStaffId = table.Column<int>(type: "integer", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOrderOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobOrderOperations_Clients_OperatedByClientId",
                        column: x => x.OperatedByClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobOrderOperations_JobOrders_JobOrderId",
                        column: x => x.JobOrderId,
                        principalTable: "JobOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobOrderOperations_Staff_HandledByStaffId",
                        column: x => x.HandledByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobOrderOperations_HandledByStaffId",
                table: "JobOrderOperations",
                column: "HandledByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrderOperations_JobOrderId",
                table: "JobOrderOperations",
                column: "JobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrderOperations_OperatedByClientId",
                table: "JobOrderOperations",
                column: "OperatedByClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobOrderOperations");
        }
    }
}
