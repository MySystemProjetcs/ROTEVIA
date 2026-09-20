using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SegregacaoNoventaENoveEEntregaPeloParceiro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "entrega_pelo_parceiro",
                table: "pedidos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "noventa_e_nove_app_shop_id",
                table: "merchants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_merchants_noventa_e_nove_app_shop_id",
                table: "merchants",
                column: "noventa_e_nove_app_shop_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_merchants_noventa_e_nove_app_shop_id",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "entrega_pelo_parceiro",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "noventa_e_nove_app_shop_id",
                table: "merchants");
        }
    }
}
