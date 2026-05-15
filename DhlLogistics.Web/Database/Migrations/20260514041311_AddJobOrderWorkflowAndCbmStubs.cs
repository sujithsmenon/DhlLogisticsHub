using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddJobOrderWorkflowAndCbmStubs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyBranches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyBranches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ActivityName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    ShipmentMode = table.Column<int>(type: "int", nullable: false),
                    ShipmentType = table.Column<int>(type: "int", nullable: false),
                    CargoType = table.Column<int>(type: "int", nullable: false),
                    JobOrderNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    JobOrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinYear = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    BillingClientId = table.Column<int>(type: "int", nullable: false),
                    ShipperId = table.Column<int>(type: "int", nullable: false),
                    ConsigneeId = table.Column<int>(type: "int", nullable: false),
                    SaleStaffId = table.Column<int>(type: "int", nullable: true),
                    LoadPortId = table.Column<int>(type: "int", nullable: true),
                    DischargePortId = table.Column<int>(type: "int", nullable: true),
                    CommodityId = table.Column<int>(type: "int", nullable: true),
                    CargoDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContainerSizeId = table.Column<int>(type: "int", nullable: true),
                    LclUnits = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: true),
                    LclUnitType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GrossWeightKg = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: true),
                    VolumeCbm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: true),
                    CurrencyId = table.Column<int>(type: "int", nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsNominated = table.Column<bool>(type: "bit", nullable: false),
                    IsEmergency = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClosedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobOrders_Clients_BillingClientId",
                        column: x => x.BillingClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOrders_Clients_ConsigneeId",
                        column: x => x.ConsigneeId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOrders_Clients_ShipperId",
                        column: x => x.ShipperId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOrders_Commodities_CommodityId",
                        column: x => x.CommodityId,
                        principalTable: "Commodities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobOrders_CompanyBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "CompanyBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobOrders_ContainerSizes_ContainerSizeId",
                        column: x => x.ContainerSizeId,
                        principalTable: "ContainerSizes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobOrders_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobOrders_Ports_DischargePortId",
                        column: x => x.DischargePortId,
                        principalTable: "Ports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOrders_Ports_LoadPortId",
                        column: x => x.LoadPortId,
                        principalTable: "Ports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobOrders_Staff_SaleStaffId",
                        column: x => x.SaleStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "JobOrderEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobOrderId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Actor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOrderEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobOrderEvents_JobOrders_JobOrderId",
                        column: x => x.JobOrderId,
                        principalTable: "JobOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyBranches_BranchCode",
                table: "CompanyBranches",
                column: "BranchCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobOrderEvents_JobOrderId_At",
                table: "JobOrderEvents",
                columns: new[] { "JobOrderId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_BillingClientId",
                table: "JobOrders",
                column: "BillingClientId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_BranchId",
                table: "JobOrders",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_CommodityId",
                table: "JobOrders",
                column: "CommodityId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_ConsigneeId",
                table: "JobOrders",
                column: "ConsigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_ContainerSizeId",
                table: "JobOrders",
                column: "ContainerSizeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_CurrencyId",
                table: "JobOrders",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_DischargePortId",
                table: "JobOrders",
                column: "DischargePortId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_JobOrderNo",
                table: "JobOrders",
                column: "JobOrderNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_LoadPortId",
                table: "JobOrders",
                column: "LoadPortId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_Mode_FinYear",
                table: "JobOrders",
                columns: new[] { "Mode", "FinYear" });

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_SaleStaffId",
                table: "JobOrders",
                column: "SaleStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_ShipperId",
                table: "JobOrders",
                column: "ShipperId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOrders_Status",
                table: "JobOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentActivities_ActivityCode",
                table: "ShipmentActivities",
                column: "ActivityCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobOrderEvents");

            migrationBuilder.DropTable(
                name: "ShipmentActivities");

            migrationBuilder.DropTable(
                name: "JobOrders");

            migrationBuilder.DropTable(
                name: "CompanyBranches");
        }
    }
}
