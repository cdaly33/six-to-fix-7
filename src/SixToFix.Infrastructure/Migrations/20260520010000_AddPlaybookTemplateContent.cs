using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SixToFix.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybookTemplateContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "playbook_templates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<string>(
                name: "content_markdown",
                table: "playbook_templates",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "content_markdown",
                table: "playbook_templates");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "playbook_templates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }
    }
}
