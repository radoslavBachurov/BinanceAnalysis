using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BinanceAnalysis.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketPrices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TradeId = table.Column<long>(type: "bigint", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketPrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonitoredSymbols",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoredSymbols", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketPrices_Symbol_ObservedAtUtc",
                table: "MarketPrices",
                columns: new[] { "Symbol", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketPrices_Symbol_TradeId",
                table: "MarketPrices",
                columns: new[] { "Symbol", "TradeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoredSymbols_Symbol",
                table: "MonitoredSymbols",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketPrices");

            migrationBuilder.DropTable(
                name: "MonitoredSymbols");
        }
    }
}
