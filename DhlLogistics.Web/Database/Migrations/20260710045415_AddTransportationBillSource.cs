using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportationBillSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwbOrBlNumber",
                table: "Bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommodityName",
                table: "Bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContainerNumber",
                table: "Bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryLocation",
                table: "Bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Destination",
                table: "Bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverName",
                table: "Bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "Bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupLocation",
                table: "Bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "Bills",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipmentTypeName",
                table: "Bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourceId",
                table: "Bills",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "Bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "Bills",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransporterId",
                table: "Bills",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleNumber",
                table: "Bills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VolumeCbm",
                table: "Bills",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                table: "Bills",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bills_SourceType_SourceId",
                table: "Bills",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_TransporterId",
                table: "Bills",
                column: "TransporterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_Transporters_TransporterId",
                table: "Bills",
                column: "TransporterId",
                principalTable: "Transporters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Transporters_TransporterId",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_Bills_SourceType_SourceId",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_Bills_TransporterId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "AwbOrBlNumber",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CommodityName",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ContainerNumber",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "DeliveryLocation",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "Destination",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "DriverName",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "PickupLocation",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ShipmentTypeName",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "TransporterId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "VehicleNumber",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "VolumeCbm",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                table: "Bills");
        }
    }
}
