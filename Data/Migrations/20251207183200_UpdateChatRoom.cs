using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnChuyenNganh.Migrations
{
    /// <inheritdoc />
    public partial class UpdateChatRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "ChatRooms");

            migrationBuilder.AddColumn<bool>(
                name: "DeletedByAdmin",
                table: "ChatRooms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DeletedByCustomer",
                table: "ChatRooms",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedByAdmin",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "DeletedByCustomer",
                table: "ChatRooms");

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "ChatRooms",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
