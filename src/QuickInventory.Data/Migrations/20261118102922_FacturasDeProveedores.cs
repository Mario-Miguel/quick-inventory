using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QuickInventory.Data.Migrations
{
    /// <inheritdoc />
    public partial class FacturasDeProveedores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CashMovementId",
                table: "InvoiceFiles",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "PurchaseInvoiceId",
                table: "InvoiceFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Providers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LegalName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Nif = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    InvoiceFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Canceled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoices_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceFiles_PurchaseInvoiceId",
                table: "InvoiceFiles",
                column: "PurchaseInvoiceId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceFiles_UnSoloDueno",
                table: "InvoiceFiles",
                sql: "num_nonnulls(\"CashMovementId\", \"PurchaseInvoiceId\") = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Providers_Nif",
                table: "Providers",
                column: "Nif",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_ProviderId_Number",
                table: "PurchaseInvoices",
                columns: new[] { "ProviderId", "Number" },
                unique: true,
                filter: "\"Canceled\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_RegistrationDate",
                table: "PurchaseInvoices",
                column: "RegistrationDate");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceFiles_PurchaseInvoices_PurchaseInvoiceId",
                table: "InvoiceFiles",
                column: "PurchaseInvoiceId",
                principalTable: "PurchaseInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceFiles_PurchaseInvoices_PurchaseInvoiceId",
                table: "InvoiceFiles");

            migrationBuilder.DropTable(
                name: "PurchaseInvoices");

            migrationBuilder.DropTable(
                name: "Providers");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceFiles_PurchaseInvoiceId",
                table: "InvoiceFiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceFiles_UnSoloDueno",
                table: "InvoiceFiles");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceId",
                table: "InvoiceFiles");

            migrationBuilder.AlterColumn<int>(
                name: "CashMovementId",
                table: "InvoiceFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
