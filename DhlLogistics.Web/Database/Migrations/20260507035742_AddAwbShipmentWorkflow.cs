using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAwbShipmentWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transporters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WhatsAppNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transporters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AwbShipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HawbNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StationCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShipperAccount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShipperName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShipperAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShipperPhone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShipperContact = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConsigneeAccount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConsigneeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConsigneeAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConsigneePhone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConsigneeContact = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginStation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DestinationStation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferenceNumbers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HandlingInfo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pieces = table.Column<int>(type: "int", nullable: false),
                    GrossWeightKg = table.Column<double>(type: "float", nullable: false),
                    ChargeableWeight = table.Column<double>(type: "float", nullable: false),
                    RateClass = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GoodsDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HsCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Dimensions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VolumeCbm = table.Column<double>(type: "float", nullable: false),
                    Slac = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeclaredValueCarriage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeclaredValueCustoms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceFile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TransporterId = table.Column<int>(type: "int", nullable: true),
                    PickupLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DropLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransporterAssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VehicleNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DriverName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DriverMobile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VehicleDetailsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAtPortAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveryPhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GodownReceiptPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomsDocsReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomsDocPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvoiceSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvoiceFilePath = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwbShipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwbShipments_Transporters_TransporterId",
                        column: x => x.TransporterId,
                        principalTable: "Transporters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ShipmentEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AwbShipmentId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentEvents_AwbShipments_AwbShipmentId",
                        column: x => x.AwbShipmentId,
                        principalTable: "AwbShipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AwbShipments_TransporterId",
                table: "AwbShipments",
                column: "TransporterId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentEvents_AwbShipmentId",
                table: "ShipmentEvents",
                column: "AwbShipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentEvents");

            migrationBuilder.DropTable(
                name: "AwbShipments");

            migrationBuilder.DropTable(
                name: "Transporters");
        }
    }
}
