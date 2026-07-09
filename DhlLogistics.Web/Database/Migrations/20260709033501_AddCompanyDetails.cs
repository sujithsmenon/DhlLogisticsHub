using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tagline = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AddressLine1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AddressLine2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Pincode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Mobile = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Website = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    GSTIN = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PAN = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CIN = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    IEC = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LogoPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BankName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AccountName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IFSC = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SwiftCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Branch = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AuthorisedSignatory = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TermsAndConditions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InvoiceFooter = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyDetails", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyDetails");
        }
    }
}
