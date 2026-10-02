const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface RecipeSummary {
  id: string;
  title: string;
  slug: string;
  description: string | null;
  difficulty: string;
  cookTimeMinutes: number;
  servings: number;
  status: string | number;
  categoryId: string;
  authorId: string;
  rowVersion: string;
}

export interface RecipeCategory {
  id: string;
  name: string;
  slug: string;
}

export async function searchRecipes(
  query: URLSearchParams,
  signal: AbortSignal,
): Promise<PaginatedResult<RecipeSummary>> {
  const params = new URLSearchParams(query);
  const search = params.get("search");
  params.delete("search");
  if (search !== null) params.set("q", search);
  const response = await fetch(`${API_BASE_URL}/api/v1/recipes/search?${params.toString()}`, { signal });
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    const validationMessages = problem?.errors
      ? Object.values(problem.errors).flat().join(" ")
      : "";
    throw new Error(validationMessages || problem?.detail || problem?.title || "Không thể tải công thức.");
  }
  return response.json();
}

export async function getRecipeCategories(signal: AbortSignal): Promise<RecipeCategory[]> {
  const response = await fetch(`${API_BASE_URL}/api/v1/categories/?pageSize=100`, { signal });
  if (!response.ok) throw new Error("Không thể tải danh mục.");
  const result = await response.json() as PaginatedResult<RecipeCategory>;
  return result.items;
}
