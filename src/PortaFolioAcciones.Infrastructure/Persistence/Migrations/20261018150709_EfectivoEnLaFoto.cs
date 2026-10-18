using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortaFolioAcciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EfectivoEnLaFoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CashValue_Amount",
                table: "PortfolioSnapshots",
                type: "numeric(28,4)",
                precision: 28,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CashValue_Currency",
                table: "PortfolioSnapshots",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashValue_Amount",
                table: "PortfolioSnapshots");

            migrationBuilder.DropColumn(
                name: "CashValue_Currency",
                table: "PortfolioSnapshots");
        }
    }
}
