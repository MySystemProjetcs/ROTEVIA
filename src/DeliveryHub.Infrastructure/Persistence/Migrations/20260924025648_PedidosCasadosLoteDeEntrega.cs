using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PedidosCasadosLoteDeEntrega : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "lote_entrega_id",
                table: "pedidos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ordem_na_rota",
                table: "pedidos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lote_entrega_id",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "ordem_na_rota",
                table: "pedidos");
        }
    }
}
