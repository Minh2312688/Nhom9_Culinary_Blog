using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lab03AddRecipeFullTextSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_Recipes_Title_Description_Fts""
                  ON ""Recipes""
                  USING GIN (
                      to_tsvector(
                          'simple',
                          ""Title"" || ' ' || coalesce(""Description"", '')
                      )
                  );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ""IX_Recipes_Title_Description_Fts"";");
        }
    }
}
