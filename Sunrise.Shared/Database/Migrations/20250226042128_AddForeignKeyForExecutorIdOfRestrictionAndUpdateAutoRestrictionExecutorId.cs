using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeyForExecutorIdOfRestrictionAndUpdateAutoRestrictionExecutorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: Previously we used "-1" for the auto-restrictions, 
            // and as -1 id doesn't really point to user,
            // we will replace it with null.
            migrationBuilder.UpdateData(
                table: "restriction",
                keyColumn: "ExecutorId",
                keyValue: -1,
                column: "ExecutorId",
                value: null
            );
            
            migrationBuilder.CreateIndex(
                name: "IX_restriction_ExecutorId",
                table: "restriction",
                column: "ExecutorId");

            migrationBuilder.AddForeignKey(
                name: "FK_restriction_user_ExecutorId",
                table: "restriction",
                column: "ExecutorId",
                principalTable: "user",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NOTE: Reverts change above
            migrationBuilder.UpdateData(
                table: "restriction",
                keyColumn: "ExecutorId",
                keyValue: null,
                column: "ExecutorId",
                value: -1
            );
            
            migrationBuilder.DropForeignKey(
                name: "FK_restriction_user_ExecutorId",
                table: "restriction");

            migrationBuilder.DropIndex(
                name: "IX_restriction_ExecutorId",
                table: "restriction");
        }
    }
}
