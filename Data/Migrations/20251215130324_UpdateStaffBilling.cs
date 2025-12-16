using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnChuyenNganh.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStaffBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "StaffBillings");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "StaffBillings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "StaffBillings");

            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "StaffBillings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
