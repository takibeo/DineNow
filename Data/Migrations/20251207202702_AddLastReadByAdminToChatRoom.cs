using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnChuyenNganh.Migrations
{
    /// <inheritdoc />
    public partial class AddLastReadByAdminToChatRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReadByAdmin",
                table: "ChatRooms",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastReadByAdmin",
                table: "ChatRooms");
        }
    }
}
