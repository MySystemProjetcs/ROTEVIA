using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IFoodMerchantContextSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ifood_merchant_snapshots",
                columns: table => new
                {
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "jsonb", nullable: false),
                    opening_hours = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ifood_merchant_snapshots", x => x.merchant_id);
                    table.ForeignKey(
                        name: "FK_ifood_merchant_snapshots_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalTable: "merchants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ifood_merchant_snapshots");
        }
    }
}
