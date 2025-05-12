using Microsoft.EntityFrameworkCore.Migrations;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultWeeklyUserHypesInTheirInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                                   INSERT INTO user_inventory_item (UserId, ItemType, Quantity)
                                   SELECT u.Id, {(int)ItemType.Hype}, {Configuration.UserHypesWeekly}
                                   FROM user u
                                   WHERE NOT EXISTS (
                                        SELECT 1
                                        FROM user_inventory_item ui
                                        WHERE ui.UserId = u.Id AND ui.ItemType = {(int)ItemType.Hype}
                                     );
                                   """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
