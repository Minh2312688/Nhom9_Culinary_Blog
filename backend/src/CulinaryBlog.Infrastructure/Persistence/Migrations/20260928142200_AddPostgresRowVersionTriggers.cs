using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CulinaryBlog.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AuthDbContext))]
[Migration("20260928142200_AddPostgresRowVersionTriggers")]
public sealed class AddPostgresRowVersionTriggers : Migration
{
    private static readonly (string Table, string Trigger)[] Tables =
    [
        ("Categories", "TR_Categories_RowVersion"),
        ("Recipes", "TR_Recipes_RowVersion"),
        ("RecipeIngredients", "TR_RecipeIngredients_RowVersion"),
        ("RecipeSteps", "TR_RecipeSteps_RowVersion"),
        ("RecipeImages", "TR_RecipeImages_RowVersion"),
        ("RecipeNutritions", "TR_RecipeNutritions_RowVersion")
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE FUNCTION public.set_base_entity_row_version()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                NEW."RowVersion" := decode(md5(random()::text || clock_timestamp()::text || NEW."Id"::text), 'hex');
                RETURN NEW;
            END;
            $$;
            """);

        foreach (var (table, trigger) in Tables)
        {
            migrationBuilder.Sql($"""
                CREATE TRIGGER "{trigger}"
                BEFORE UPDATE ON "{table}"
                FOR EACH ROW
                EXECUTE FUNCTION public.set_base_entity_row_version();
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var (table, trigger) in Tables)
            migrationBuilder.Sql($"DROP TRIGGER \"{trigger}\" ON \"{table}\";");

        migrationBuilder.Sql("DROP FUNCTION public.set_base_entity_row_version();");
    }
}
