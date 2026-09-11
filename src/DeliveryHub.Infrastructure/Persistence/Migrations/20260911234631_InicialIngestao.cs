using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InicialIngestao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_inbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    external_event_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    external_merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_inbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "merchants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ifood_merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pedidos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_externo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    eh_teste = table.Column<bool>(type: "boolean", nullable: false),
                    recebido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedidos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_pendentes",
                table: "integration_inbox",
                column: "received_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_inbox_event",
                table: "integration_inbox",
                columns: new[] { "source", "external_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_merchants_ifood_id",
                table: "merchants",
                column: "ifood_merchant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_ativos_por_merchant",
                table: "pedidos",
                columns: new[] { "merchant_id", "recebido_em" },
                filter: "status IN ('Recebido','Confirmado','EmPreparo','Pronto','Despachado')");

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_id_externo",
                table: "pedidos",
                column: "id_externo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_inbox");

            migrationBuilder.DropTable(
                name: "merchants");

            migrationBuilder.DropTable(
                name: "pedidos");
        }
    }
}
