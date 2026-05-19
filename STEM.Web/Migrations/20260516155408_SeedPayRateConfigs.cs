using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace STEM.Web.Migrations
{
    /// <inheritdoc />
    public partial class SeedPayRateConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "PayRateConfigs",
                columns: new[] { "Id", "CourseDifficulty", "RatePerSession", "TeacherTier" },
                values: new object[,]
                {
                    { 1, 1, 100000m, 1 },
                    { 2, 2, 120000m, 1 },
                    { 3, 3, 0m, 1 },
                    { 4, 1, 150000m, 2 },
                    { 5, 2, 180000m, 2 },
                    { 6, 3, 200000m, 2 },
                    { 7, 1, 200000m, 3 },
                    { 8, 2, 250000m, 3 },
                    { 9, 3, 300000m, 3 },
                    { 10, 1, 250000m, 4 },
                    { 11, 2, 300000m, 4 },
                    { 12, 3, 400000m, 4 },
                    { 13, 1, 300000m, 5 },
                    { 14, 2, 400000m, 5 },
                    { 15, 3, 500000m, 5 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "PayRateConfigs",
                keyColumn: "Id",
                keyValue: 15);
        }
    }
}
