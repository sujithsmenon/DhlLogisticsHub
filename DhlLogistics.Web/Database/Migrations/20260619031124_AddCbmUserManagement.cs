using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCbmUserManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegisterdUsers",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AspNetUserId = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    FullName = table.Column<string>(type: "text", nullable: true),
                    StaffId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisterdUsers", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_RegisterdUsers_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserCompanyBranchPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCompanyBranchPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCompanyBranchPermissions_CompanyBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "CompanyBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCompanyBranchPermissions_RegisterdUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "RegisterdUsers",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserShipmentActivityPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ActivityId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserShipmentActivityPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserShipmentActivityPermissions_RegisterdUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "RegisterdUsers",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserShipmentActivityPermissions_ShipmentActivities_Activity~",
                        column: x => x.ActivityId,
                        principalTable: "ShipmentActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegisterdUsers_AspNetUserId",
                table: "RegisterdUsers",
                column: "AspNetUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegisterdUsers_StaffId",
                table: "RegisterdUsers",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyBranchPermissions_BranchId",
                table: "UserCompanyBranchPermissions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyBranchPermissions_UserId_BranchId",
                table: "UserCompanyBranchPermissions",
                columns: new[] { "UserId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserShipmentActivityPermissions_ActivityId",
                table: "UserShipmentActivityPermissions",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShipmentActivityPermissions_UserId_ActivityId",
                table: "UserShipmentActivityPermissions",
                columns: new[] { "UserId", "ActivityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCompanyBranchPermissions");

            migrationBuilder.DropTable(
                name: "UserShipmentActivityPermissions");

            migrationBuilder.DropTable(
                name: "RegisterdUsers");
        }
    }
}
