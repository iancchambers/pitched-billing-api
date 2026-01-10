using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitchedBillingApi.Migrations
{
    /// <inheritdoc />
    public partial class RenameInvoiceHistoryToInvoiceAndAddItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailDeliveryStatuses_InvoiceHistories_InvoiceHistoryId",
                table: "EmailDeliveryStatuses");

            migrationBuilder.DropTable(
                name: "InvoiceHistories");

            migrationBuilder.RenameColumn(
                name: "InvoiceHistoryId",
                table: "EmailDeliveryStatuses",
                newName: "InvoiceId");

            migrationBuilder.RenameIndex(
                name: "IX_EmailDeliveryStatuses_InvoiceHistoryId",
                table: "EmailDeliveryStatuses",
                newName: "IX_EmailDeliveryStatuses_InvoiceId");

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuickBooksInvoiceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PdfContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PostedToQuickBooksDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerCompanyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillToLine1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillToCity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillToCounty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillToPostCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillToCountry = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    YourReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OurReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountHandler = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_BillingPlans_BillingPlanId",
                        column: x => x.BillingPlanId,
                        principalTable: "BillingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_InvoiceId",
                table: "InvoiceItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BillingPlanId",
                table: "Invoices",
                column: "BillingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailDeliveryStatuses_Invoices_InvoiceId",
                table: "EmailDeliveryStatuses",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailDeliveryStatuses_Invoices_InvoiceId",
                table: "EmailDeliveryStatuses");

            migrationBuilder.DropTable(
                name: "InvoiceItems");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.RenameColumn(
                name: "InvoiceId",
                table: "EmailDeliveryStatuses",
                newName: "InvoiceHistoryId");

            migrationBuilder.RenameIndex(
                name: "IX_EmailDeliveryStatuses_InvoiceId",
                table: "EmailDeliveryStatuses",
                newName: "IX_EmailDeliveryStatuses_InvoiceHistoryId");

            migrationBuilder.CreateTable(
                name: "InvoiceHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountHandler = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillToCity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillToCountry = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillToCounty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillToLine1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillToPostCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerCompanyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    GeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OurReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PdfContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PostedToQuickBooksDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QuickBooksInvoiceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    YourReference = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceHistories_BillingPlans_BillingPlanId",
                        column: x => x.BillingPlanId,
                        principalTable: "BillingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHistories_BillingPlanId",
                table: "InvoiceHistories",
                column: "BillingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHistories_InvoiceNumber",
                table: "InvoiceHistories",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailDeliveryStatuses_InvoiceHistories_InvoiceHistoryId",
                table: "EmailDeliveryStatuses",
                column: "InvoiceHistoryId",
                principalTable: "InvoiceHistories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
