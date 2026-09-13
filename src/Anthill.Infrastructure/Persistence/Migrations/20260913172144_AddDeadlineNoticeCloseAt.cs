using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeadlineNoticeCloseAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NULL cho mọi dòng đang có là đúng nghĩa "chưa nhắc lần nào": những em đang trong khoảng sắp
            // hết giờ sẽ được nhắc đúng một lần ở lượt quét đầu tiên sau khi triển khai, còn những em đã
            // hết giờ thì không lọt vào truy vấn quét.
            migrationBuilder.AddColumn<DateTime>(
                name: "DeadlineNoticeCloseAt",
                table: "ExamAssignments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeadlineNoticeCloseAt",
                table: "ExamAssignments");
        }
    }
}
