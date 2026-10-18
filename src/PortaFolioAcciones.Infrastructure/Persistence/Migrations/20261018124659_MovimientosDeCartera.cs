using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortaFolioAcciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MovimientosDeCartera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Amount_Amount = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    Amount_Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PricePerUnit_Amount = table.Column<decimal>(type: "numeric(28,6)", precision: 28, scale: 6, nullable: true),
                    PricePerUnit_Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movements_PortfolioId_OccurredOn",
                table: "Movements",
                columns: new[] { "PortfolioId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Movements_PositionId_OccurredOn",
                table: "Movements",
                columns: new[] { "PositionId", "OccurredOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Movements");
        }
    }
}
