using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuickInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class addinvoicemanagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "InvoiceDate",
                table: "Sales",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceNumber",
                table: "Sales",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceSeries",
                table: "Sales",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceType",
                table: "Sales",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RectifiedSaleId",
                table: "Sales",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxExemption",
                table: "SaleLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TaxRate",
                table: "SaleLines",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxExemption",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxRate",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvoiceSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SalesPoint = table.Column<string>(type: "text", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSeries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_InvoiceSeries_InvoiceNumber",
                table: "Sales",
                columns: new[] { "InvoiceSeries", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_RectifiedSaleId",
                table: "Sales",
                column: "RectifiedSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSeries_Code",
                table: "InvoiceSeries",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSeries_SalesPoint_Year",
                table: "InvoiceSeries",
                columns: new[] { "SalesPoint", "Year" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Sales_RectifiedSaleId",
                table: "Sales",
                column: "RectifiedSaleId",
                principalTable: "Sales",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Sales_RectifiedSaleId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "InvoiceSeries");

            migrationBuilder.DropIndex(
                name: "IX_Sales_InvoiceSeries_InvoiceNumber",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_RectifiedSaleId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "InvoiceDate",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "InvoiceSeries",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "InvoiceType",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RectifiedSaleId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TaxExemption",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "TaxExemption",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "Products");
        }
    }
}
