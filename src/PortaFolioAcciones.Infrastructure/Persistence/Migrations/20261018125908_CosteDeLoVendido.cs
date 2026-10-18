using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortaFolioAcciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CosteDeLoVendido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostPerUnit_Amount",
                table: "Movements",
                type: "numeric(28,6)",
                precision: 28,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostPerUnit_Currency",
                table: "Movements",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostPerUnit_Amount",
                table: "Movements");

            migrationBuilder.DropColumn(
                name: "CostPerUnit_Currency",
                table: "Movements");
        }
    }
}
