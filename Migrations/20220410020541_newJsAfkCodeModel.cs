using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanawanagasaki.TwitchHub.Migrations
{
    public partial class newJsAfkCodeModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "js_afk_code_model",
                columns: table => new
                {
                    Uuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    Channel = table.Column<string>(type: "TEXT", nullable: true),
                    InitCode = table.Column<string>(type: "TEXT", nullable: true),
                    TickCode = table.Column<string>(type: "TEXT", nullable: true),
                    SymbolTickCode = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_js_afk_code_model", x => x.Uuid);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "js_afk_code_model");
        }
    }
}
