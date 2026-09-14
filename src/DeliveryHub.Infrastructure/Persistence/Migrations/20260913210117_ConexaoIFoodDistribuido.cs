using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConexaoIFoodDistribuido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ifood_merchant_id",
                table: "merchants",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ifood_access_token",
                table: "merchants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ifood_authorization_code_verifier",
                table: "merchants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ifood_codigo_expira_em",
                table: "merchants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ifood_conectado_em",
                table: "merchants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ifood_refresh_token",
                table: "merchants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ifood_token_expira_em",
                table: "merchants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ifood_user_code",
                table: "merchants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ifood_access_token",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "ifood_authorization_code_verifier",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "ifood_codigo_expira_em",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "ifood_conectado_em",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "ifood_refresh_token",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "ifood_token_expira_em",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "ifood_user_code",
                table: "merchants");

            migrationBuilder.AlterColumn<Guid>(
                name: "ifood_merchant_id",
                table: "merchants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
