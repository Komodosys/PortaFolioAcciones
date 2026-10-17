using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortaFolioAcciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FullDomainModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QuoteCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exchange = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Isin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    ValuationMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PricingFrequency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BrokerCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LatestQuotes",
                columns: table => new
                {
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOfUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DelayMinutes = table.Column<int>(type: "integer", nullable: true),
                    MarketState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Price_Amount = table.Column<decimal>(type: "numeric(28,6)", precision: 28, scale: 6, nullable: false),
                    Price_Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LatestQuotes", x => x.InstrumentId);
                });

            migrationBuilder.CreateTable(
                name: "Portfolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Portfolios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioSnapshots",
                columns: table => new
                {
                    PortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CostBasis_Amount = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    CostBasis_Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MarketValue_Amount = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    MarketValue_Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioSnapshots", x => new { x.PortfolioId, x.SnapshotDate });
                });

            migrationBuilder.CreateTable(
                name: "Prices",
                columns: table => new
                {
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Close_Amount = table.Column<decimal>(type: "numeric(28,6)", precision: 28, scale: 6, nullable: false),
                    Close_Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prices", x => new { x.InstrumentId, x.PriceDate });
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PreferredLanguage = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_BrokerCode",
                table: "Instruments",
                column: "BrokerCode");

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Identity",
                table: "Instruments",
                columns: new[] { "Symbol", "Exchange", "QuoteCurrency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_ProviderKey",
                table: "Instruments",
                column: "ProviderKey");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_OwnerUserId",
                table: "Portfolios",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_PortfolioId_SnapshotDate",
                table: "PortfolioSnapshots",
                columns: new[] { "PortfolioId", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Prices_InstrumentId_PriceDate",
                table: "Prices",
                columns: new[] { "InstrumentId", "PriceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "\"Email\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Instruments");

            migrationBuilder.DropTable(
                name: "LatestQuotes");

            migrationBuilder.DropTable(
                name: "Portfolios");

            migrationBuilder.DropTable(
                name: "PortfolioSnapshots");

            migrationBuilder.DropTable(
                name: "Prices");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
