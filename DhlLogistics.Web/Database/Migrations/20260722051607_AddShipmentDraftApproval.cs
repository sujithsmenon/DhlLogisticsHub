using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentDraftApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShipmentDraftApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IncomingEmailId = table.Column<int>(type: "integer", nullable: false),
                    EmailSubject = table.Column<string>(type: "text", nullable: false),
                    ShipmentType = table.Column<string>(type: "text", nullable: true),
                    Direction = table.Column<string>(type: "text", nullable: true),
                    Customer = table.Column<string>(type: "text", nullable: true),
                    DhlInvoiceNumber = table.Column<string>(type: "text", nullable: true),
                    ContainerNumber = table.Column<string>(type: "text", nullable: true),
                    Hawb = table.Column<string>(type: "text", nullable: true),
                    Mawb = table.Column<string>(type: "text", nullable: true),
                    BlNumber = table.Column<string>(type: "text", nullable: true),
                    OriginPort = table.Column<string>(type: "text", nullable: true),
                    DestinationPort = table.Column<string>(type: "text", nullable: true),
                    Eta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Etd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceNumbers = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    HighConfidence = table.Column<bool>(type: "boolean", nullable: false),
                    ExtractionNotes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<string>(type: "text", nullable: true),
                    ReviewNotes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentDraftApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentDraftApprovals_IncomingEmails_IncomingEmailId",
                        column: x => x.IncomingEmailId,
                        principalTable: "IncomingEmails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentDraftApprovals_DhlInvoiceNumber",
                table: "ShipmentDraftApprovals",
                column: "DhlInvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentDraftApprovals_IncomingEmailId",
                table: "ShipmentDraftApprovals",
                column: "IncomingEmailId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentDraftApprovals_Status",
                table: "ShipmentDraftApprovals",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentDraftApprovals");
        }
    }
}
