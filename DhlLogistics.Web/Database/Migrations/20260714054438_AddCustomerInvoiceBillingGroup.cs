using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerInvoiceBillingGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CustomerInvoiceId",
                table: "InvoiceDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CustomerInvoiceId",
                table: "Bills",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerInvoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceNo = table.Column<string>(type: "text", nullable: false),
                    FinYear = table.Column<int>(type: "integer", nullable: false),
                    CustomerInvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BillingClientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    CurrencyId = table.Column<int>(type: "integer", nullable: true),
                    SubTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GstAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaymentTerms = table.Column<string>(type: "text", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CancelledOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<string>(type: "text", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerInvoices_Clients_BillingClientId",
                        column: x => x.BillingClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerInvoices_CompanyBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "CompanyBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerInvoices_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocuments_CustomerInvoiceId_IsActive",
                table: "InvoiceDocuments",
                columns: new[] { "CustomerInvoiceId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_CustomerInvoiceId",
                table: "Bills",
                column: "CustomerInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvoices_BillingClientId",
                table: "CustomerInvoices",
                column: "BillingClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvoices_BranchId",
                table: "CustomerInvoices",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvoices_CurrencyId",
                table: "CustomerInvoices",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvoices_CustomerInvoiceNumber",
                table: "CustomerInvoices",
                column: "CustomerInvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvoices_FinYear",
                table: "CustomerInvoices",
                column: "FinYear");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvoices_InvoiceNo",
                table: "CustomerInvoices",
                column: "InvoiceNo",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_CustomerInvoices_CustomerInvoiceId",
                table: "Bills",
                column: "CustomerInvoiceId",
                principalTable: "CustomerInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceDocuments_CustomerInvoices_CustomerInvoiceId",
                table: "InvoiceDocuments",
                column: "CustomerInvoiceId",
                principalTable: "CustomerInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_CustomerInvoices_CustomerInvoiceId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceDocuments_CustomerInvoices_CustomerInvoiceId",
                table: "InvoiceDocuments");

            migrationBuilder.DropTable(
                name: "CustomerInvoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceDocuments_CustomerInvoiceId_IsActive",
                table: "InvoiceDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Bills_CustomerInvoiceId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CustomerInvoiceId",
                table: "InvoiceDocuments");

            migrationBuilder.DropColumn(
                name: "CustomerInvoiceId",
                table: "Bills");
        }
    }
}
