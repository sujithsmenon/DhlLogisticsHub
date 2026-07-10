using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUniversalSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SearchAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserName = table.Column<string>(type: "text", nullable: true),
                    Term = table.Column<string>(type: "text", nullable: false),
                    ModuleHint = table.Column<string>(type: "text", nullable: true),
                    ResultCount = table.Column<int>(type: "integer", nullable: false),
                    ElapsedMs = table.Column<int>(type: "integer", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpenedModule = table.Column<string>(type: "text", nullable: true),
                    OpenedPrimary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_PlateNumber",
                table: "Vehicles",
                column: "PlateNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_JobReference",
                table: "ExportJobs",
                column: "JobReference");

            migrationBuilder.CreateIndex(
                name: "IX_Containers_ContainerNumber",
                table: "Containers",
                column: "ContainerNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_InvoiceNumber",
                table: "Bills",
                column: "InvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AwbShipments_HawbNo",
                table: "AwbShipments",
                column: "HawbNo");

            migrationBuilder.CreateIndex(
                name: "IX_SearchAuditLogs_At",
                table: "SearchAuditLogs",
                column: "At");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_PlateNumber",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_ExportJobs_JobReference",
                table: "ExportJobs");

            migrationBuilder.DropIndex(
                name: "IX_Containers_ContainerNumber",
                table: "Containers");

            migrationBuilder.DropIndex(
                name: "IX_Bills_InvoiceNumber",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_AwbShipments_HawbNo",
                table: "AwbShipments");
        }
    }
}
