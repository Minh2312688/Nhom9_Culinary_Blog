import type { Metadata, ResolvingMetadata } from "next";
import type { RecipeDetail } from "@/lib/recipes-api";
import { buildRecipeJsonLd, serializeRecipeJsonLd } from "@/lib/recipe-json-ld";
import { buildRecipeMetadata } from "@/lib/recipe-metadata";
import { getSiteUrl } from "@/lib/site-config";
import { getPublicRecipe, PublicRecipeApiError } from "@/lib/server/recipes-api";
import { RecipeDetailClient, RecipeDetailError } from "./recipe-detail-client";

export async function generateMetadata(
  {
    params,
  }: {
    params: Promise<{ slug: string }>;
  },
  parent: ResolvingMetadata,
): Promise<Metadata> {
  try {
    const { slug } = await params;
    const recipe = await getPublicRecipe(slug);
    const metadata = buildRecipeMetadata(recipe, getSiteUrl());

    // When recipe has no images, inherit parent's OG/Twitter images
    // (the root opengraph-image.tsx / twitter-image.tsx defaults).
    // Next.js replaces the entire openGraph object, so we must carry
    // parent images forward explicitly.
    const hasRecipeImages = recipe.images?.some((img) => img.originalUrl);
    if (!hasRecipeImages) {
      const parentMetadata = await parent;
      const parentOgImages = parentMetadata.openGraph?.images;
      const parentTwitterImages = parentMetadata.twitter?.images;
      if (parentOgImages && metadata.openGraph && typeof metadata.openGraph === "object") {
        (metadata.openGraph as Record<string, unknown>).images = parentOgImages;
      }
      if (parentTwitterImages && metadata.twitter && typeof metadata.twitter === "object") {
        (metadata.twitter as Record<string, unknown>).images = parentTwitterImages;
      }
    }

    return metadata;
  } catch {
    return {
      title: "Công thức",
      robots: {
        index: false,
      },
    };
  }
}

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
