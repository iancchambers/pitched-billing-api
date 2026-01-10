using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitchedBillingApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressFieldsToInvoiceHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountHandler",
                table: "InvoiceHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillToCity",
                table: "InvoiceHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillToCountry",
                table: "InvoiceHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillToCounty",
                table: "InvoiceHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillToLine1",
                table: "InvoiceHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillToPostCode",
                table: "InvoiceHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerCompanyName",
                table: "InvoiceHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                table: "InvoiceHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "InvoiceHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OurReference",
                table: "InvoiceHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YourReference",
                table: "InvoiceHistories",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountHandler",
                table: "InvoiceHistories");

            migrationBuilder.DropColumn(
                name: "BillToCity",
                table: "InvoiceHistories");

            migrationBuilder.DropColumn(
                name: "BillToCountry",
                table: "InvoiceHistories");

            migrationBuilder.DropColumn(
                name: "BillToCounty",
                table: "InvoiceHistories");

            migrationBuilder.DropColumn(
                name: "BillToLine1",
                table: "InvoiceHistories");

            migrationBuilder.DropColumn(
                name: "BillToPostCode",
                table: "InvoiceHistories");

            migrationBuilder.DropColumn(
                name: "CustomerCompanyName",
                table: "InvoiceHistories");

            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                table: "InvoiceHistories");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "InvoiceHistories");

            migrationBuilder.DropColumn(
                name: "OurReference",
                table: "InvoiceHistories");

            migrationBuilder.DropColumn(
                name: "YourReference",
                table: "InvoiceHistories");
        }
    }
}
