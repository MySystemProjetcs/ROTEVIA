using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PagamentoDoPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pagamento_descricao",
                table: "pedidos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "pagamento_valor_a_cobrar",
                table: "pedidos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "pagamento_valor_pago",
                table: "pedidos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // O novo status entra no filtro do índice parcial, senão pedido em
            // "Cobrar" ficaria fora dele e o painel passaria a varrer a tabela.
            migrationBuilder.DropIndex(
                name: "ix_pedidos_ativos_por_merchant",
                table: "pedidos");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_ativos_por_merchant",
                table: "pedidos",
                columns: new[] { "merchant_id", "recebido_em" },
                filter: "status IN ('Recebido','Confirmado','EmPreparo','Pronto','Despachado','Aceito','EmRota','Chegou','Cobrar')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pagamento_descricao",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "pagamento_valor_a_cobrar",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "pagamento_valor_pago",
                table: "pedidos");

            migrationBuilder.DropIndex(
                name: "ix_pedidos_ativos_por_merchant",
                table: "pedidos");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_ativos_por_merchant",
                table: "pedidos",
                columns: new[] { "merchant_id", "recebido_em" },
                filter: "status IN ('Recebido','Confirmado','EmPreparo','Pronto','Despachado','Aceito','EmRota','Chegou')");
        }
    }
}
