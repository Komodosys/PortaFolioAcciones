using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortaFolioAcciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConversionDeMoneda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CounterAmount_Amount",
                table: "Movements",
                type: "numeric(28,4)",
                precision: 28,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterAmount_Currency",
                table: "Movements",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CounterAmount_Amount",
                table: "Movements");

            migrationBuilder.DropColumn(
                name: "CounterAmount_Currency",
                table: "Movements");
        }
    }
}
