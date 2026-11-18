using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuickInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlbaranesProveedores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceFiles_UnSoloDueno",
                table: "InvoiceFiles");

            migrationBuilder.AddColumn<int>(
                name: "DeliveryNoteId",
                table: "StockMovements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryNoteId",
                table: "PurchaseInvoiceLines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryNoteId",
                table: "InvoiceFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeliveryNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "date", nullable: false),
                    BaseAmount = table.Column<long>(type: "bigint", nullable: false),
                    TaxAmount = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseInvoiceId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RegistrationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Canceled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_PurchaseInvoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryNoteLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeliveryNoteId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<long>(type: "bigint", nullable: false),
                    TaxRate = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNoteLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryNoteLines_DeliveryNotes_DeliveryNoteId",
                        column: x => x.DeliveryNoteId,
                        principalTable: "DeliveryNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryNoteLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_DeliveryNoteId",
                table: "StockMovements",
                column: "DeliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceLines_DeliveryNoteId",
                table: "PurchaseInvoiceLines",
                column: "DeliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceFiles_DeliveryNoteId",
                table: "InvoiceFiles",
                column: "DeliveryNoteId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceFiles_UnSoloDueno",
                table: "InvoiceFiles",
                sql: "num_nonnulls(\"CashMovementId\", \"PurchaseInvoiceId\", \"DeliveryNoteId\") = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_DeliveryNoteId",
                table: "DeliveryNoteLines",
                column: "DeliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_ProductId",
                table: "DeliveryNoteLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_DeliveryDate",
                table: "DeliveryNotes",
                column: "DeliveryDate");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_ProviderId_Number",
                table: "DeliveryNotes",
                columns: new[] { "ProviderId", "Number" },
                unique: true,
                filter: "\"Canceled\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_PurchaseInvoiceId",
                table: "DeliveryNotes",
                column: "PurchaseInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceFiles_DeliveryNotes_DeliveryNoteId",
                table: "InvoiceFiles",
                column: "DeliveryNoteId",
                principalTable: "DeliveryNotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceLines_DeliveryNotes_DeliveryNoteId",
                table: "PurchaseInvoiceLines",
                column: "DeliveryNoteId",
                principalTable: "DeliveryNotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_DeliveryNotes_DeliveryNoteId",
                table: "StockMovements",
                column: "DeliveryNoteId",
                principalTable: "DeliveryNotes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceFiles_DeliveryNotes_DeliveryNoteId",
                table: "InvoiceFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoiceLines_DeliveryNotes_DeliveryNoteId",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_DeliveryNotes_DeliveryNoteId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "DeliveryNoteLines");

            migrationBuilder.DropTable(
                name: "DeliveryNotes");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_DeliveryNoteId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoiceLines_DeliveryNoteId",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceFiles_DeliveryNoteId",
                table: "InvoiceFiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceFiles_UnSoloDueno",
                table: "InvoiceFiles");

            migrationBuilder.DropColumn(
                name: "DeliveryNoteId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "DeliveryNoteId",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropColumn(
                name: "DeliveryNoteId",
                table: "InvoiceFiles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceFiles_UnSoloDueno",
                table: "InvoiceFiles",
                sql: "num_nonnulls(\"CashMovementId\", \"PurchaseInvoiceId\") = 1");
        }
    }
}
