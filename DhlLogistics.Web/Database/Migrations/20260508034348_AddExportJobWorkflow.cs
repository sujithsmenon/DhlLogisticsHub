using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddExportJobWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExportJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobReference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CargoDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HsCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pieces = table.Column<int>(type: "int", nullable: false),
                    GrossWeightKg = table.Column<double>(type: "float", nullable: false),
                    IsEmergency = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PickupInitiatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransporterId = table.Column<int>(type: "int", nullable: true),
                    TransporterBookedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VehicleNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DriverName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DriverMobile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VehicleAssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RiNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CargoCollectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChecklistRef = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChecklistSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IcegateRef = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShippingBillNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShippingBillAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OperationStaffName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HandedOverAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PortArrivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PortClearedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExportReadyAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BookingReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BookingReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SurveyTeamName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SurveyForwardedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SurveyCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LoadingAuthReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LoadingAuthAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContainerNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SealNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CargoLoadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShippingInstruction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryOrderRef = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThirdPartyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SiDoReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InTransitToIcttAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Sez4Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VesselName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VoyageNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TerminalGateInAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportJobs_Transporters_TransporterId",
                        column: x => x.TransporterId,
                        principalTable: "Transporters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExportJobEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExportJobId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportJobEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportJobEvents_ExportJobs_ExportJobId",
                        column: x => x.ExportJobId,
                        principalTable: "ExportJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobEvents_ExportJobId",
                table: "ExportJobEvents",
                column: "ExportJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_TransporterId",
                table: "ExportJobs",
                column: "TransporterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportJobEvents");

            migrationBuilder.DropTable(
                name: "ExportJobs");
        }
    }
}
