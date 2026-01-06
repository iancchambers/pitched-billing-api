using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitchedBillingApi.Migrations
{
    /// <inheritdoc />
    public partial class AddQuickBooksToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuickBooksTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RealmId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccessToken = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RefreshTokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuickBooksTokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuickBooksTokens_RealmId",
                table: "QuickBooksTokens",
                column: "RealmId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuickBooksTokens");
        }
    }
}
