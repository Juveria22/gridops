using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GridOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutageIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_CrewId",
                table: "WorkOrders");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_CrewId_Status",
                table: "WorkOrders",
                columns: new[] { "CrewId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Outages_Borough_ReportedAt",
                table: "Outages",
                columns: new[] { "Borough", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Outages_Priority_ReportedAt",
                table: "Outages",
                columns: new[] { "Priority", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Outages_ReportedAt",
                table: "Outages",
                column: "ReportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Outages_Status_ReportedAt",
                table: "Outages",
                columns: new[] { "Status", "ReportedAt" })
                .Annotation("SqlServer:Include", new[] { "Borough", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_CrewId_Status",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_Outages_Borough_ReportedAt",
                table: "Outages");

            migrationBuilder.DropIndex(
                name: "IX_Outages_Priority_ReportedAt",
                table: "Outages");

            migrationBuilder.DropIndex(
                name: "IX_Outages_ReportedAt",
                table: "Outages");

            migrationBuilder.DropIndex(
                name: "IX_Outages_Status_ReportedAt",
                table: "Outages");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_CrewId",
                table: "WorkOrders",
                column: "CrewId");
        }
    }
}
