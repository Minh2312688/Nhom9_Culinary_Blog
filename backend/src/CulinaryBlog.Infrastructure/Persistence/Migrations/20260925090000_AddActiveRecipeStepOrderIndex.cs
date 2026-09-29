using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260925090000_AddActiveRecipeStepOrderIndex")]
public partial class AddActiveRecipeStepOrderIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Title",
            table: "RecipeSteps",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(150)",
            oldMaxLength: 150,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_RecipeSteps_RecipeId_StepNumber",
            table: "RecipeSteps",
            columns: new[] { "RecipeId", "StepNumber" },
            unique: true,
            filter: "\"IsDeleted\" = false");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_RecipeSteps_RecipeId_StepNumber",
            table: "RecipeSteps");

        migrationBuilder.AlterColumn<string>(
            name: "Title",
            table: "RecipeSteps",
            type: "character varying(150)",
            maxLength: 150,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(200)",
            oldMaxLength: 200,
            oldNullable: true);
    }
}
