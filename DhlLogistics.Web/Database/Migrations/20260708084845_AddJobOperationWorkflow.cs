using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhlLogistics.Web.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddJobOperationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // JobOperationStatus was redefined (Draft/Assigned/InProgress/Completed/Cancelled). The stored
            // integers 0/20/30/40 keep their meaning; only the old "InProgress = 10" moves to 20 so those
            // rows stay In Progress instead of being reinterpreted as the new "Assigned = 10".
            migrationBuilder.Sql(@"UPDATE ""JobOperations"" SET ""Status"" = 20 WHERE ""Status"" = 10;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort inverse (In Progress → old 10). Note: rows that were originally OnHold (20) are
            // indistinguishable from In Progress after Up, so this maps all 20s back to 10.
            migrationBuilder.Sql(@"UPDATE ""JobOperations"" SET ""Status"" = 10 WHERE ""Status"" = 20;");
        }
    }
}
