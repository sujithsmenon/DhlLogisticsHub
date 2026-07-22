using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftShipmentLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedShipmentId",
                table: "ShipmentDraftApprovals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedShipmentType",
                table: "ShipmentDraftApprovals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShipmentCreatedAt",
                table: "ShipmentDraftApprovals",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedShipmentId",
                table: "ShipmentDraftApprovals");

            migrationBuilder.DropColumn(
                name: "CreatedShipmentType",
                table: "ShipmentDraftApprovals");

            migrationBuilder.DropColumn(
                name: "ShipmentCreatedAt",
                table: "ShipmentDraftApprovals");
        }
    }
}
