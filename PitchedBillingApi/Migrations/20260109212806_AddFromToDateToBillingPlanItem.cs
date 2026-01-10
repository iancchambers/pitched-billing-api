using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitchedBillingApi.Migrations
{
    /// <inheritdoc />
    public partial class AddFromToDateToBillingPlanItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FromDate",
                table: "BillingPlanItems",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ToDate",
                table: "BillingPlanItems",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromDate",
                table: "BillingPlanItems");

            migrationBuilder.DropColumn(
                name: "ToDate",
                table: "BillingPlanItems");
        }
    }
}
