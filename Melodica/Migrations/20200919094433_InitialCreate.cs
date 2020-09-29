using Microsoft.EntityFrameworkCore.Migrations;

namespace Melodica.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateTable(
                name: "GuildSettings",
                columns: table => new
                {
                    GuildID = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Prefix = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildSettings", x => x.GuildID);
                });

        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(
                name: "GuildSettings");
    }
}
