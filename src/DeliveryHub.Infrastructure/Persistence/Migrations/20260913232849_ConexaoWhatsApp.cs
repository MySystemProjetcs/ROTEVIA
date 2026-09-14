using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConexaoWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "whatsapp_conectado_em",
                table: "merchants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "whatsapp_status",
                table: "merchants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "whatsapp_telefone",
                table: "merchants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "whatsapp_conectado_em",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "whatsapp_status",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "whatsapp_telefone",
                table: "merchants");
        }
    }
}
