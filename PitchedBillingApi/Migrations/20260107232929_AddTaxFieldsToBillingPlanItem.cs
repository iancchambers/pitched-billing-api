using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitchedBillingApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxFieldsToBillingPlanItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuickBooksTaxCodeId",
                table: "BillingPlanItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "BillingPlanItems",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuickBooksTaxCodeId",
                table: "BillingPlanItems");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "BillingPlanItems");
        }
    }
}
