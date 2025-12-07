using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnChuyenNganh.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedByUserIdToChatRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "ChatRooms",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "ChatRooms");
        }
    }
}
