import type { RecipeDetail } from "@/lib/recipes-api";
import { buildRecipeJsonLd, serializeRecipeJsonLd } from "@/lib/recipe-json-ld";
import { getPublicRecipe, PublicRecipeApiError } from "@/lib/server/recipes-api";
import { RecipeDetailClient, RecipeDetailError } from "./recipe-detail-client";

export default async function RecipeDetailPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  let recipe: RecipeDetail;
  try {
    recipe = await getPublicRecipe(slug);
  } catch (error) {
    return (
      <RecipeDetailError
        message={error instanceof PublicRecipeApiError && error.status === 404
          ? "Không tìm thấy công thức."
          : "Không thể tải công thức. Vui lòng thử lại sau."}
      />
    );
  }

  const jsonLd = serializeRecipeJsonLd(buildRecipeJsonLd(recipe));
  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: jsonLd }}
      />
      <RecipeDetailClient recipe={recipe} />
    </>
  );
}
