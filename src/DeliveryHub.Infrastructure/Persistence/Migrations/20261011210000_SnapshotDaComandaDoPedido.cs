using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotDaComandaDoPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // JSONB nullable: pedidos recebidos antes desta migração ficam
            // sem snapshot até a próxima reimpressão, que cai no fallback
            // via ComandaBuilder.Montar em memória (PedidoConfiguration.cs).
            migrationBuilder.AddColumn<string>(
                name: "receipt_data",
                table: "pedidos",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "receipt_data",
                table: "pedidos");
        }
    }
}
