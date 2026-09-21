using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRowVersionDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RecipeSteps",
                type: "bytea",
                nullable: false,
                defaultValueSql: "decode('00', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Recipes",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "decode('00', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RecipeNutritions",
                type: "bytea",
                nullable: false,
                defaultValueSql: "decode('00', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RecipeIngredients",
                type: "bytea",
                nullable: false,
                defaultValueSql: "decode('00', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RecipeImages",
                type: "bytea",
                nullable: false,
                defaultValueSql: "decode('00', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RecipeSteps",
                type: "bytea",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldDefaultValueSql: "decode('00', 'hex')");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Recipes",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldDefaultValueSql: "decode('00', 'hex')");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RecipeNutritions",
                type: "bytea",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldDefaultValueSql: "decode('00', 'hex')");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RecipeIngredients",
                type: "bytea",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldDefaultValueSql: "decode('00', 'hex')");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RecipeImages",
                type: "bytea",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldDefaultValueSql: "decode('00', 'hex')");
        }
    }
}
