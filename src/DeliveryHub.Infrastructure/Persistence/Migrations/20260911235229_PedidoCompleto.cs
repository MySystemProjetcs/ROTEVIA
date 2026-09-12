using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PedidoCompleto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cliente_localizador",
                table: "pedidos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cliente_nome",
                table: "pedidos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "cliente_telefone",
                table: "pedidos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "criado_na_origem_em",
                table: "pedidos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "entrega_bairro",
                table: "pedidos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrega_cep",
                table: "pedidos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrega_cidade",
                table: "pedidos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrega_complemento",
                table: "pedidos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrega_estado",
                table: "pedidos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "entrega_latitude",
                table: "pedidos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrega_logradouro",
                table: "pedidos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "entrega_longitude",
                table: "pedidos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrega_numero",
                table: "pedidos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrega_referencia",
                table: "pedidos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_exibicao",
                table: "pedidos",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "taxa_entrega",
                table: "pedidos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_total",
                table: "pedidos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "pedido_itens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    indice = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    unidade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    preco_unitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    preco_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedido_itens", x => x.id);
                    table.ForeignKey(
                        name: "FK_pedido_itens_pedidos_pedido_id",
                        column: x => x.pedido_id,
                        principalTable: "pedidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pedido_itens_pedido",
                table: "pedido_itens",
                column: "pedido_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pedido_itens");

            migrationBuilder.DropColumn(
                name: "cliente_localizador",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "cliente_nome",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "cliente_telefone",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "criado_na_origem_em",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entrega_bairro",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entrega_cep",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entrega_cidade",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entrega_complemento",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entrega_estado",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entrega_latitude",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entrega_logradouro",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entrega_longitude",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entrega_numero",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "entrega_referencia",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "numero_exibicao",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "taxa_entrega",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "valor_total",
                table: "pedidos");
        }
    }
}
