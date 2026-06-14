using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusForever.Database.Character.Migrations
{
    public partial class AddCharacterBuffTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_buff",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint(20) unsigned", nullable: false, defaultValue: 0ul),
                    spell4BaseId = table.Column<uint>(type: "int(10) unsigned", nullable: false, defaultValue: 0u),
                    casterGuid = table.Column<uint>(type: "int(10) unsigned", nullable: false, defaultValue: 0u),
                    stackCount = table.Column<uint>(type: "int(10) unsigned", nullable: false, defaultValue: 0u),
                    durationRemaining = table.Column<double>(type: "double", nullable: false, defaultValue: 0d),
                    tickRemaining = table.Column<double>(type: "double", nullable: false, defaultValue: 0d)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => new { x.id, x.spell4BaseId });
                    table.ForeignKey(
                        name: "FK_character_buff_id__character_id",
                        column: x => x.id,
                        principalTable: "character",
                        principalColumn: "id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_buff");
        }
    }
}
