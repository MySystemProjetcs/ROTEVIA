using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantNosItens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "merchant_id",
                table: "pedido_itens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Sem isto os itens já gravados ficariam com merchant zerado, ou
            // seja, invisíveis para a loja dona deles e para todo mundo.
            migrationBuilder.Sql("""
                UPDATE pedido_itens i
                SET merchant_id = p.merchant_id
                FROM pedidos p
                WHERE p.id = i.pedido_id;
                """);

            // O default só existia para permitir a coluna NOT NULL na tabela
            // com dados; daqui em diante o valor vem sempre do pedido.
            migrationBuilder.Sql("ALTER TABLE pedido_itens ALTER COLUMN merchant_id DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_itens_merchant",
                table: "pedido_itens",
                column: "merchant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_pedido_itens_merchant", table: "pedido_itens");

            migrationBuilder.DropColumn(
                name: "merchant_id",
                table: "pedido_itens");
        }
    }
}
