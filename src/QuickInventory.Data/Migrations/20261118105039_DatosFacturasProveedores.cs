using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class DatosFacturasProveedores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_RegistrationDate",
                table: "PurchaseInvoices");

            migrationBuilder.AddColumn<long>(
                name: "BaseAmount",
                table: "PurchaseInvoices",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PurchaseInvoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoiceDate",
                table: "PurchaseInvoices",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "PurchaseInvoices",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDate",
                table: "PurchaseInvoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "PurchaseInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TaxAmount",
                table: "PurchaseInvoices",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "TaxRate",
                table: "PurchaseInvoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Facturas apuntadas antes de tener estos campos (solo puede haberlas en desarrollo):
            // se les da valores que la app sepa leer. Sin desglose de IVA, todo el importe va a la base.
            migrationBuilder.Sql("""
                UPDATE "PurchaseInvoices"
                SET "InvoiceDate" = "RegistrationDate"::date,
                    "Description" = 'Sin concepto',
                    "TaxRate" = '0',
                    "BaseAmount" = "Amount",
                    "TaxAmount" = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_InvoiceDate",
                table: "PurchaseInvoices",
                column: "InvoiceDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_InvoiceDate",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "BaseAmount",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "InvoiceDate",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentDate",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "PurchaseInvoices");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_RegistrationDate",
                table: "PurchaseInvoices",
                column: "RegistrationDate");
        }
    }
}
