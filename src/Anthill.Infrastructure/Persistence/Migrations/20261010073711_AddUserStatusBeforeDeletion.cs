using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStatusBeforeDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatusBeforeDeletion",
                table: "Users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusBeforeDeletion",
                table: "Users");
        }
    }
}
