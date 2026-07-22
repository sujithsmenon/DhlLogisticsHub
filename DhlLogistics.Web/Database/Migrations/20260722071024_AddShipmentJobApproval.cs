using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentJobApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShipmentJobApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShipmentDraftApprovalId = table.Column<int>(type: "integer", nullable: false),
                    ShipmentKind = table.Column<string>(type: "text", nullable: false),
                    ShipmentId = table.Column<int>(type: "integer", nullable: false),
                    DhlInvoiceNumber = table.Column<string>(type: "text", nullable: true),
                    CustomerName = table.Column<string>(type: "text", nullable: true),
                    EmailSubject = table.Column<string>(type: "text", nullable: false),
                    ProposedMode = table.Column<int>(type: "integer", nullable: false),
                    ShipmentMode = table.Column<int>(type: "integer", nullable: false),
                    ShipmentDirection = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<string>(type: "text", nullable: true),
                    ReviewNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedJobOrderId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentJobApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentJobApprovals_ShipmentDraftApprovals_ShipmentDraftAp~",
                        column: x => x.ShipmentDraftApprovalId,
                        principalTable: "ShipmentDraftApprovals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentJobApprovals_ShipmentDraftApprovalId",
                table: "ShipmentJobApprovals",
                column: "ShipmentDraftApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentJobApprovals_ShipmentKind_ShipmentId",
                table: "ShipmentJobApprovals",
                columns: new[] { "ShipmentKind", "ShipmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentJobApprovals_Status",
                table: "ShipmentJobApprovals",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentJobApprovals");
        }
    }
}
