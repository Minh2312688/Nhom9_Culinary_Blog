using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignAuthDbContextRecipeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecipeSteps_RecipeId_StepNumber",
                table: "RecipeSteps");

            migrationBuilder.RenameColumn(
                name: "TimerMinutes",
                table: "RecipeSteps",
                newName: "DurationMinutes");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "RecipeSteps",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RecipeSteps",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "decode('00', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Recipes",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "decode('00', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RecipeIngredients",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "decode('00', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Categories",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "decode('00', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "RecipeImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MediumUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AltText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false, defaultValueSql: "decode('00', 'hex')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeImages_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeNutritions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Calories = table.Column<int>(type: "integer", nullable: false),
                    Protein = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    Carbohydrates = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    Fat = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    Fiber = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    Sodium = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false, defaultValueSql: "decode('00', 'hex')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeNutritions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeNutritions_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSteps_RecipeId_StepNumber",
                table: "RecipeSteps",
                columns: new[] { "RecipeId", "StepNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeImages_RecipeId",
                table: "RecipeImages",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeNutritions_RecipeId",
                table: "RecipeNutritions",
                column: "RecipeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeImages");

            migrationBuilder.DropTable(
                name: "RecipeNutritions");

            migrationBuilder.DropIndex(
                name: "IX_RecipeSteps_RecipeId_StepNumber",
                table: "RecipeSteps");

            migrationBuilder.RenameColumn(
                name: "DurationMinutes",
                table: "RecipeSteps",
                newName: "TimerMinutes");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "RecipeSteps",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RecipeSteps",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldDefaultValueSql: "decode('00', 'hex')");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Recipes",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldDefaultValueSql: "decode('00', 'hex')");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RecipeIngredients",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldDefaultValueSql: "decode('00', 'hex')");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Categories",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldDefaultValueSql: "decode('00', 'hex')");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSteps_RecipeId_StepNumber",
                table: "RecipeSteps",
                columns: new[] { "RecipeId", "StepNumber" },
                unique: true);
        }
    }
}
