using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementParsing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementNameToStatement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatementName",
                table: "Statements",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatementName",
                table: "Statements");
        }
    }
}
