using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260930120000_AddRecipeFullTextSearch")]
public partial class AddRecipeFullTextSearch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
        migrationBuilder.Sql("ALTER TABLE \"Recipes\" ADD COLUMN \"SearchVector\" tsvector;");
        migrationBuilder.Sql("""
            UPDATE "Recipes"
            SET "SearchVector" = to_tsvector('simple', unaccent(coalesce("Title", '') || ' ' || coalesce("Description", '')));
            """);
        migrationBuilder.Sql("ALTER TABLE \"Recipes\" ALTER COLUMN \"SearchVector\" SET NOT NULL;");
        migrationBuilder.Sql("CREATE INDEX \"IDX_Recipe_Search\" ON \"Recipes\" USING GIN (\"SearchVector\");");
        migrationBuilder.Sql("""
            CREATE FUNCTION update_recipe_search_vector() RETURNS trigger AS $$
            BEGIN
                NEW."SearchVector" := to_tsvector('simple', unaccent(coalesce(NEW."Title", '') || ' ' || coalesce(NEW."Description", '')));
                RETURN NEW;
            END
            $$ LANGUAGE plpgsql;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER trg_recipes_search_vector
            BEFORE INSERT OR UPDATE OF "Title", "Description" ON "Recipes"
            FOR EACH ROW EXECUTE FUNCTION update_recipe_search_vector();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_recipes_search_vector ON \"Recipes\";");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_recipe_search_vector();");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IDX_Recipe_Search\";");
        migrationBuilder.Sql("ALTER TABLE \"Recipes\" DROP COLUMN IF EXISTS \"SearchVector\";");
    }
}
