using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class SerieUnicaPorAnio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceSeries_SalesPoint_Year",
                table: "InvoiceSeries");

            migrationBuilder.DropColumn(
                name: "SalesPoint",
                table: "InvoiceSeries");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSeries_Year",
                table: "InvoiceSeries",
                column: "Year",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceSeries_Year",
                table: "InvoiceSeries");

            migrationBuilder.AddColumn<string>(
                name: "SalesPoint",
                table: "InvoiceSeries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSeries_SalesPoint_Year",
                table: "InvoiceSeries",
                columns: new[] { "SalesPoint", "Year" },
                unique: true);
        }
    }
}
