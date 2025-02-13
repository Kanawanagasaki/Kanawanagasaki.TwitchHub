using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanawanagasaki.TwitchHub.Migrations
{
    /// <inheritdoc />
    public partial class Settings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "setting",
                columns: table => new
                {
                    Uuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: true),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_setting", x => x.Uuid);
                });

            migrationBuilder.CreateIndex(
                name: "IX_setting_Key",
                table: "setting",
                column: "Key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "setting");
        }
    }
}
