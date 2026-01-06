using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitchedBillingApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillingPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    QuickBooksCustomerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BillingPlanItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuickBooksItemId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingPlanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingPlanItems_BillingPlans_BillingPlanId",
                        column: x => x.BillingPlanId,
                        principalTable: "BillingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceHistories",
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
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
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

            migrationBuilder.CreateTable(
                name: "EmailDeliveryStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceHistoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MailgunMessageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDeliveryStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailDeliveryStatuses_InvoiceHistories_InvoiceHistoryId",
                        column: x => x.InvoiceHistoryId,
                        principalTable: "InvoiceHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingPlanItems_BillingPlanId",
                table: "BillingPlanItems",
                column: "BillingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingPlans_QuickBooksCustomerId",
                table: "BillingPlans",
                column: "QuickBooksCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingPlans_QuickBooksCustomerId_IsActive",
                table: "BillingPlans",
                columns: new[] { "QuickBooksCustomerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryStatuses_InvoiceHistoryId",
                table: "EmailDeliveryStatuses",
                column: "InvoiceHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryStatuses_MailgunMessageId",
                table: "EmailDeliveryStatuses",
                column: "MailgunMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHistories_BillingPlanId",
                table: "InvoiceHistories",
                column: "BillingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHistories_InvoiceNumber",
                table: "InvoiceHistories",
                column: "InvoiceNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingPlanItems");

            migrationBuilder.DropTable(
                name: "EmailDeliveryStatuses");

            migrationBuilder.DropTable(
                name: "InvoiceHistories");

            migrationBuilder.DropTable(
                name: "BillingPlans");
        }
    }
}
