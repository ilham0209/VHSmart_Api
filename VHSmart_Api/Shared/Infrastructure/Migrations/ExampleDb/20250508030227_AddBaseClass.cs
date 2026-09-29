using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMAS_API_Example.Migrations.ExampleDb
{
    /// <inheritdoc />
    public partial class AddBaseClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExampleEntities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    ISDELETED = table.Column<bool>(type: "bit", nullable: false),
                    SYSUSERCREATED = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SYSDATECREATED = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SYSUSERMODIFIED = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SYSDATEMODIFIED = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExampleEntities", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExampleEntities");
        }
    }
}
