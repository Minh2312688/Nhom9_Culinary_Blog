import type { MetadataRoute } from "next";
import { getSiteUrl } from "@/lib/site-config";
import { getAllPublishedRecipes } from "@/lib/server/recipes-api";

export const dynamic = "force-dynamic";

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const siteUrl = getSiteUrl();
  const now = new Date();

  const staticRoutes: MetadataRoute.Sitemap = [
    {
      url: `${siteUrl}`,
      lastModified: now,
      changeFrequency: "daily",
      priority: 1.0,
    },
    {
      url: `${siteUrl}/recipes`,
      lastModified: now,
      changeFrequency: "weekly",
      priority: 0.8,
    },
    {
      url: `${siteUrl}/categories`,
      lastModified: now,
      changeFrequency: "weekly",
      priority: 0.6,
    },
  ];

  let recipeRoutes: MetadataRoute.Sitemap = [];
  try {
    const recipes = await getAllPublishedRecipes();
    recipeRoutes = recipes.map((recipe) => ({
      url: `${siteUrl}/recipes/${recipe.slug}`,
      lastModified: recipe.publishedAt ? new Date(recipe.publishedAt) : now,
      changeFrequency: "weekly",
      priority: 0.7,
    }));
  } catch {
    recipeRoutes = [];
  }

  return [...staticRoutes, ...recipeRoutes];
}

