using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FotoDePerfil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "foto_base64",
                table: "usuarios",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "foto_base64",
                table: "usuarios");
        }
    }
}
