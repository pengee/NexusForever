using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusForever.Database.World.Migrations
{
    /// <inheritdoc />
    public partial class RemoveKeyDefaultValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<uint>(
                name: "triggerId",
                table: "tutorial",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "type",
                table: "tutorial",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "tutorial",
                type: "int(10) unsigned",
                nullable: false,
                comment: "Tutorial ID",
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u,
                oldComment: "Tutorial ID");

            migrationBuilder.AlterColumn<byte>(
                name: "currencyId",
                table: "store_offer_item_price",
                type: "tinyint(3) unsigned",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "tinyint(3) unsigned",
                oldDefaultValue: (byte)0);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "store_offer_item_price",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<ushort>(
                name: "itemId",
                table: "store_offer_item_data",
                type: "smallint(5) unsigned",
                nullable: false,
                oldClrType: typeof(ushort),
                oldType: "smallint(5) unsigned",
                oldDefaultValue: (ushort)0);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "store_offer_item_data",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "groupId",
                table: "store_offer_item",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "store_offer_item",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "categoryId",
                table: "store_offer_group_category",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "store_offer_group_category",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "store_offer_group",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "store_category",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<uint>(
                name: "index",
                table: "entity_vendor_item",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_vendor_item",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "index",
                table: "entity_vendor_category",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_vendor_category",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_vendor",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<byte>(
                name: "stat",
                table: "entity_stats",
                type: "tinyint(3) unsigned",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "tinyint(3) unsigned",
                oldDefaultValue: (byte)0);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_stats",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_spline",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_script",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "property",
                table: "entity_property",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_property",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "phase",
                table: "entity_event",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "eventId",
                table: "entity_event",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_event",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<uint>(
                name: "objectId",
                table: "disable",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<byte>(
                name: "type",
                table: "disable",
                type: "tinyint(3) unsigned",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "tinyint(3) unsigned",
                oldDefaultValue: (byte)0);

            migrationBuilder.AlterColumn<uint>(
                name: "property",
                table: "creature_info_property",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "creature_info_property",
                type: "int(10) unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldDefaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<uint>(
                name: "triggerId",
                table: "tutorial",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "type",
                table: "tutorial",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "tutorial",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                comment: "Tutorial ID",
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned",
                oldComment: "Tutorial ID");

            migrationBuilder.AlterColumn<byte>(
                name: "currencyId",
                table: "store_offer_item_price",
                type: "tinyint(3) unsigned",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint(3) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "store_offer_item_price",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<ushort>(
                name: "itemId",
                table: "store_offer_item_data",
                type: "smallint(5) unsigned",
                nullable: false,
                defaultValue: (ushort)0,
                oldClrType: typeof(ushort),
                oldType: "smallint(5) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "store_offer_item_data",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "groupId",
                table: "store_offer_item",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "store_offer_item",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "categoryId",
                table: "store_offer_group_category",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "store_offer_group_category",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "store_offer_group",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "store_category",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<uint>(
                name: "index",
                table: "entity_vendor_item",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_vendor_item",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "index",
                table: "entity_vendor_category",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_vendor_category",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_vendor",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<byte>(
                name: "stat",
                table: "entity_stats",
                type: "tinyint(3) unsigned",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint(3) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_stats",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_spline",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_script",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "property",
                table: "entity_property",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_property",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "phase",
                table: "entity_event",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "eventId",
                table: "entity_event",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity_event",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "entity",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<uint>(
                name: "objectId",
                table: "disable",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<byte>(
                name: "type",
                table: "disable",
                type: "tinyint(3) unsigned",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint(3) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "property",
                table: "creature_info_property",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");

            migrationBuilder.AlterColumn<uint>(
                name: "id",
                table: "creature_info_property",
                type: "int(10) unsigned",
                nullable: false,
                defaultValue: 0u,
                oldClrType: typeof(uint),
                oldType: "int(10) unsigned");
        }
    }
}
