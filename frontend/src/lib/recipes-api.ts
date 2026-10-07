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

export interface RecipeIngredient {
  id?: string;
  name: string;
  quantity: number | null;
  unit: string | null;
  notes: string | null;
  orderIndex: number;
}

export interface RecipeStep {
  id?: string;
  stepNumber?: number;
  title: string | null;
  description: string;
  durationMinutes: number | null;
  imageUrl: string | null;
}

export interface RecipeDetail extends RecipeSummary {
  prepTimeMinutes: number;
  ingredients: RecipeIngredient[];
  steps: RecipeStep[];
  images: Array<{
    id: string;
    originalUrl: string;
    mediumUrl: string | null;
    thumbnailUrl: string | null;
    altText: string | null;
    isPrimary: boolean;
    orderIndex: number;
  }>;
  nutrition: {
    calories: number;
    protein: number;
    carbohydrates: number;
    fat: number;
    fiber: number;
    sodium: number;
  } | null;
}

export interface RecipeCategory {
  id: string;
  name: string;
  slug: string;
  description?: string | null;
  imageUrl?: string | null;
  orderIndex?: number;
  recipeCount?: number;
}

export interface RecipeInput {
  title: string;
  description: string | null;
  categoryId: string;
  prepTimeMinutes: number;
  cookTimeMinutes: number;
  servings: number;
  difficulty: string;
  ingredients: RecipeIngredient[];
  steps: RecipeStep[];
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, init);
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    const validationMessages = problem?.errors
      ? Object.values(problem.errors).flat().join(" ")
      : "";
    throw new Error(validationMessages || problem?.detail || problem?.title || "Yêu cầu không thành công.");
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

function authHeaders(token: string, json = false): HeadersInit {
  return {
    ...(json ? { "Content-Type": "application/json" } : {}),
    Authorization: `Bearer ${token}`,
  };
}

export async function searchRecipes(
  query: URLSearchParams,
  signal: AbortSignal,
): Promise<PaginatedResult<RecipeSummary>> {
  const params = new URLSearchParams(query);
  const search = params.get("search");
  params.delete("search");
  if (search !== null) params.set("q", search);
  return request(`/api/v1/recipes/search?${params.toString()}`, { signal });
}

export async function getRecipes(
  signal: AbortSignal,
  pageSize = 12,
  token?: string,
): Promise<PaginatedResult<RecipeSummary>> {
  return request(`/api/v1/recipes/?page=1&pageSize=${pageSize}`, {
    signal,
    headers: token ? authHeaders(token) : undefined,
  });
}

export async function getRecipe(slug: string, signal?: AbortSignal): Promise<RecipeDetail> {
  return request(`/api/v1/recipes/${encodeURIComponent(slug)}`, { signal });
}

export async function getRecipeCategories(signal: AbortSignal): Promise<RecipeCategory[]> {
  const result = await request<PaginatedResult<RecipeCategory>>(
    "/api/v1/categories/?pageSize=100",
    { signal },
  );
  return result.items;
}

export async function getCategory(slug: string, signal?: AbortSignal): Promise<RecipeCategory> {
  return request(`/api/v1/categories/${encodeURIComponent(slug)}`, { signal });
}

export async function createRecipe(token: string, payload: RecipeInput): Promise<RecipeDetail> {
  return request("/api/v1/recipes/", {
    method: "POST",
    headers: authHeaders(token, true),
    body: JSON.stringify(payload),
  });
}

export async function updateRecipe(
  token: string,
  id: string,
  payload: RecipeInput & { rowVersion: string },
): Promise<RecipeDetail> {
  return request(`/api/v1/recipes/${id}`, {
    method: "PUT",
    headers: authHeaders(token, true),
    body: JSON.stringify(payload),
  });
}

export async function deleteRecipe(token: string, id: string): Promise<void> {
  return request(`/api/v1/recipes/${id}`, {
    method: "DELETE",
    headers: authHeaders(token),
  });
}

export async function publishRecipe(token: string, id: string): Promise<void> {
  return request(`/api/v1/recipes/${id}/publish`, {
    method: "PATCH",
    headers: authHeaders(token),
  });
}

export async function createCategory(
  token: string,
  payload: { name: string; description: string | null },
): Promise<RecipeCategory> {
  return request("/api/v1/categories/", {
    method: "POST",
    headers: authHeaders(token, true),
    body: JSON.stringify(payload),
  });
}

export async function updateCategory(
  token: string,
  id: string,
  payload: { name: string; description: string | null; imageUrl: string | null; orderIndex: number },
): Promise<RecipeCategory> {
  return request(`/api/v1/categories/${id}`, {
    method: "PUT",
    headers: authHeaders(token, true),
    body: JSON.stringify(payload),
  });
}

export async function deleteCategory(token: string, id: string): Promise<void> {
  return request(`/api/v1/categories/${id}`, {
    method: "DELETE",
    headers: authHeaders(token),
  });
}
