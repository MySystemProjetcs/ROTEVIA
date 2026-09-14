using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GanhoPorEntrega : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "taxa_padrao_por_entrega",
                table: "merchants",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_pago_ao_entregador",
                table: "pedidos",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "disponivel_para_entrega",
                table: "couriers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_merchant_recebido",
                table: "pedidos",
                columns: new[] { "merchant_id", "recebido_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pedidos_merchant_recebido",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "disponivel_para_entrega",
                table: "couriers");

            migrationBuilder.DropColumn(
                name: "valor_pago_ao_entregador",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "taxa_padrao_por_entrega",
                table: "merchants");
        }
    }
}
