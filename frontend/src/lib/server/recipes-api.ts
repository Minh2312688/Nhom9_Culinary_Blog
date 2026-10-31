import "server-only";
import type { PaginatedResult, RecipeDetail, RecipeSummary } from "../recipes-api";

const API_BASE_URL =
  process.env.API_INTERNAL_URL ||
  process.env.NEXT_PUBLIC_API_URL ||
  "http://localhost:5000";

export class PublicRecipeApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = "PublicRecipeApiError";
  }
}

export async function getPublicRecipe(slug: string): Promise<RecipeDetail> {
  const response = await fetch(
    `${API_BASE_URL}/api/v1/recipes/${encodeURIComponent(slug)}`,
    { cache: "no-store" },
  );

  if (!response.ok) {
    throw new PublicRecipeApiError(response.status, "Không tìm thấy công thức.");
  }

  const recipe = (await response.json()) as RecipeDetail;
  if (recipe.status !== "Published" && recipe.status !== 1) {
    throw new PublicRecipeApiError(404, "Không tìm thấy công thức.");
  }

  return recipe;
}

export async function getAllPublishedRecipes(): Promise<RecipeSummary[]> {
  const allRecipes: RecipeSummary[] = [];
  const pageSize = 50;
  const maxPages = 20;

  for (let page = 1; page <= maxPages; page++) {
    const response = await fetch(
      `${API_BASE_URL}/api/v1/recipes/?page=${page}&pageSize=${pageSize}`,
      { cache: "no-store" },
    );

    if (!response.ok) {
      throw new Error(`Failed to fetch published recipes: ${response.status} ${response.statusText}`);
    }

    const data = (await response.json()) as PaginatedResult<RecipeSummary>;
    const published = (data.items || []).filter(
      (item) => item.status === "Published" || item.status === 1,
    );
    allRecipes.push(...published);

    if (!data.hasNextPage) {
      break;
    }
  }

  return allRecipes;
}
