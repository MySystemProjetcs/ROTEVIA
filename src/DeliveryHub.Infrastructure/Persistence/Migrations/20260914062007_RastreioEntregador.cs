using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RastreioEntregador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "endereco_bairro",
                table: "merchants",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cep",
                table: "merchants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade",
                table: "merchants",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_complemento",
                table: "merchants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_estado",
                table: "merchants",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "endereco_latitude",
                table: "merchants",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_logradouro",
                table: "merchants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "endereco_longitude",
                table: "merchants",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_numero",
                table: "merchants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_referencia",
                table: "merchants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "posicoes_entregador",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entregador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    precisao_metros = table.Column<double>(type: "double precision", nullable: false),
                    capturado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recebido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posicoes_entregador", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_posicoes_por_pedido",
                table: "posicoes_entregador",
                columns: new[] { "pedido_id", "capturado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "posicoes_entregador");

            migrationBuilder.DropColumn(
                name: "endereco_bairro",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "endereco_cep",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "endereco_cidade",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "endereco_complemento",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "endereco_estado",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "endereco_latitude",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "endereco_logradouro",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "endereco_longitude",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "endereco_numero",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "endereco_referencia",
                table: "merchants");
        }
    }
}
