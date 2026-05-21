using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STEM.Web.Migrations
{
    /// <inheritdoc />
    public partial class UniqueSessionClassNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                WITH CTE AS (
                    SELECT Id,
                           ROW_NUMBER() OVER(PARTITION BY ClassId, SessionNo ORDER BY Id) as RowNum
                    FROM Sessions
                )
                DELETE FROM Sessions WHERE Id IN (SELECT Id FROM CTE WHERE RowNum > 1);
            ");

            migrationBuilder.CreateIndex(
                name: "UQ_Sessions_ClassId_SessionNo",
                table: "Sessions",
                columns: new[] { "ClassId", "SessionNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Sessions_ClassId_SessionNo",
                table: "Sessions");
        }
    }
}
