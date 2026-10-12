using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncWithCurrentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_inbox_pendentes",
                table: "integration_inbox");

            migrationBuilder.AddColumn<string>(
                name: "receipt_data",
                table: "pedidos",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "impressoras_da_loja",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    modelo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    largura = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    setor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    imprimir_automaticamente = table.Column<bool>(type: "boolean", nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_impressoras_da_loja", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_entregador_status",
                table: "pedidos",
                columns: new[] { "entregador_id", "status" },
                filter: "entregador_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_inbox_pendentes",
                table: "integration_inbox",
                columns: new[] { "source", "received_at" },
                filter: "processed_at IS NULL AND merchant_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_impressoras_merchant",
                table: "impressoras_da_loja",
                column: "merchant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "impressoras_da_loja");

            migrationBuilder.DropIndex(
                name: "ix_pedidos_entregador_status",
                table: "pedidos");

            migrationBuilder.DropIndex(
                name: "ix_inbox_pendentes",
                table: "integration_inbox");

            migrationBuilder.DropColumn(
                name: "receipt_data",
                table: "pedidos");

            migrationBuilder.CreateIndex(
                name: "ix_inbox_pendentes",
                table: "integration_inbox",
                column: "received_at",
                filter: "processed_at IS NULL");
        }
    }
}
