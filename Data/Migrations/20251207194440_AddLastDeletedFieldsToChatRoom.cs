using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnChuyenNganh.Migrations
{
    /// <inheritdoc />
    public partial class AddLastDeletedFieldsToChatRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastDeletedByAdmin",
                table: "ChatRooms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDeletedByCustomer",
                table: "ChatRooms",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastDeletedByAdmin",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "LastDeletedByCustomer",
                table: "ChatRooms");
        }
    }
}
