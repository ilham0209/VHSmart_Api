using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMAS_API_Example.Migrations
{
    /// <inheritdoc />
    public partial class AddCarColour : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Colour",
                table: "CarEntities",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Colour",
                table: "CarEntities");
        }
    }
}
