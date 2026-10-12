using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppId99NoventaENove : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "app_id_99",
                table: "merchants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "app_id_99",
                table: "merchants");
        }
    }
}
