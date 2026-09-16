using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xmin là cột hệ thống PostgreSQL tự có trên mọi bảng. Npgsql nhận ra tên này và không sinh
            // câu ALTER TABLE nào, nên migration chỉ ghi nhận concurrency token vào model; chạy trên
            // database đã có dữ liệu không đổi gì ở lược đồ.
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Users",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Users");
        }
    }
}
