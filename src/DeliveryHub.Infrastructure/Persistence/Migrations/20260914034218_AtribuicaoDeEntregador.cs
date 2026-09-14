using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AtribuicaoDeEntregador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pedidos_ativos_por_merchant",
                table: "pedidos");

            migrationBuilder.AddColumn<Guid>(
                name: "entregador_id",
                table: "pedidos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_ativos_por_merchant",
                table: "pedidos",
                columns: new[] { "merchant_id", "recebido_em" },
                filter: "status IN ('Recebido','Confirmado','EmPreparo','Pronto','Despachado','Aceito','EmRota','Chegou')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pedidos_ativos_por_merchant",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entregador_id",
                table: "pedidos");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_ativos_por_merchant",
                table: "pedidos",
                columns: new[] { "merchant_id", "recebido_em" },
                filter: "status IN ('Recebido','Confirmado','EmPreparo','Pronto','Despachado')");
        }
    }
}
