using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanawanagasaki.TwitchHub.Migrations
{
    /// <inheritdoc />
    public partial class TwitchCustomRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "twitch_custom_reward",
                columns: table => new
                {
                    Uuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthUuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    BotAuthUuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsCreated = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwitchId = table.Column<string>(type: "TEXT", nullable: true),
                    RewardType = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Cost = table.Column<int>(type: "INTEGER", nullable: false),
                    IsUserInputRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    Prompt = table.Column<string>(type: "TEXT", nullable: true),
                    BackgroundColor = table.Column<string>(type: "TEXT", nullable: true),
                    Extra = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_twitch_custom_reward", x => x.Uuid);
                });

            migrationBuilder.CreateIndex(
                name: "IX_twitch_custom_reward_AuthUuid",
                table: "twitch_custom_reward",
                column: "AuthUuid");

            migrationBuilder.CreateIndex(
                name: "IX_twitch_custom_reward_TwitchId",
                table: "twitch_custom_reward",
                column: "TwitchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "twitch_custom_reward");
        }
    }
}
