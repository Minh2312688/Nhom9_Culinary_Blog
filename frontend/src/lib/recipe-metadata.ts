import type { Metadata } from "next";
import type { RecipeDetail } from "./recipes-api";

export function buildRecipeMetadata(
  recipe: RecipeDetail,
  siteUrl: string,
): Metadata {
  const cleanSiteUrl = siteUrl.replace(/\/+$/, "");
  const selectedImage =
    recipe.images?.find((img) => img.isPrimary) ?? recipe.images?.[0];

  const images = selectedImage
    ? [
        {
          url: selectedImage.originalUrl,
          ...(selectedImage.altText ? { alt: selectedImage.altText } : {}),
        },
      ]
    : undefined;

  return {
    title: recipe.title,
    description: recipe.description ?? undefined,
    openGraph: {
      siteName: "Culinary Blog",
      locale: "vi_VN",
      type: "article",
      url: `${cleanSiteUrl}/recipes/${recipe.slug}`,
      title: recipe.title,
      description: recipe.description ?? undefined,
      ...(images ? { images } : {}),
    },
    twitter: {
      card: "summary_large_image",
      title: recipe.title,
      description: recipe.description ?? undefined,
      ...(images ? { images } : {}),
    },
  };
}

