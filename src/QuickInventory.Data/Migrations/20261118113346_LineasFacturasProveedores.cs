using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuickInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class LineasFacturasProveedores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PurchaseInvoiceId",
                table: "StockMovements",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PurchaseInvoiceId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<long>(type: "bigint", nullable: false),
                    TaxRate = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceLines_PurchaseInvoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Facturas apuntadas antes de tener líneas (solo puede haberlas en desarrollo): su concepto
            // pasa a ser una línea libre con toda la base y su IVA, así los totales siguen cuadrando.
            migrationBuilder.Sql("""
                INSERT INTO "PurchaseInvoiceLines" ("PurchaseInvoiceId", "ProductId", "Description", "Quantity", "UnitPrice", "TaxRate")
                SELECT "Id", NULL, "Description", 1, "BaseAmount", "TaxRate"
                FROM "PurchaseInvoices";
                """);

            migrationBuilder.DropColumn(
                name: "Description",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "PurchaseInvoices");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PurchaseInvoiceId",
                table: "StockMovements",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceLines_ProductId",
                table: "PurchaseInvoiceLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceLines_PurchaseInvoiceId",
                table: "PurchaseInvoiceLines",
                column: "PurchaseInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_PurchaseInvoices_PurchaseInvoiceId",
                table: "StockMovements",
                column: "PurchaseInvoiceId",
                principalTable: "PurchaseInvoices",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_PurchaseInvoices_PurchaseInvoiceId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceLines");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_PurchaseInvoiceId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceId",
                table: "StockMovements");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PurchaseInvoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaxRate",
                table: "PurchaseInvoices",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
