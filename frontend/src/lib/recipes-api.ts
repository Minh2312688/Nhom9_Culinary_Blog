import { redirectToLoginForCurrentPage } from "./auth-redirect";

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
  publishedAt: string | null;
  primaryImageUrl?: string | null;
  primaryImageAltText?: string | null;
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
    stepNumber: number | null;
  }>;
  nutrition: {
    calories: number;
    protein: number;
    carbohydrates: number;
    fat: number;
    fiber: number;
    sodium: number;
  } | null;
  author: {
    id: string;
    displayName: string;
    avatarUrl: string | null;
  } | null;
}

export interface AuthorProfilePage {
  id: string;
  displayName: string;
  avatarUrl: string | null;
  bio: string | null;
  recipes: PaginatedResult<RecipeSummary>;
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

export interface RecipeNutritionInput {
  calories: number;
  protein: number;
  carbohydrates: number;
  fat: number;
  fiber: number;
  sodium: number;
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
  nutrition?: RecipeNutritionInput | null;
}

export interface ApiProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export class ApiRequestError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ApiProblemDetails | undefined,
    fallbackMessage: string,
  ) {
    const validationMessages = Object.values(problem?.errors ?? {}).flat();
    super(problem?.detail || problem?.title || validationMessages.join(" ") || fallbackMessage);
    this.name = "ApiRequestError";
  }
}

function parseProblemDetails(value: unknown): ApiProblemDetails | undefined {
  if (!value || typeof value !== "object" || Array.isArray(value)) return undefined;

  const source = value as Record<string, unknown>;
  const errors = source.errors;
  const parsedErrors = errors && typeof errors === "object" && !Array.isArray(errors)
    ? Object.fromEntries(
      Object.entries(errors).flatMap(([key, messages]) => {
        const values = Array.isArray(messages) ? messages : [messages];
        const strings = values.filter((message): message is string => typeof message === "string");
        return strings.length ? [[key, strings]] : [];
      }),
    )
    : undefined;

  return {
    ...(typeof source.title === "string" ? { title: source.title } : {}),
    ...(typeof source.detail === "string" ? { detail: source.detail } : {}),
    ...(parsedErrors ? { errors: parsedErrors } : {}),
  };
}

async function request<T>(url: string, init?: RequestInit, isPrivate = false): Promise<T> {
  const response = await fetch(url, init);
  if (!response.ok) {
    if (isPrivate && response.status === 401) redirectToLoginForCurrentPage();
    let problemBody: unknown;
    try {
      problemBody = await response.json();
    } catch {
      problemBody = undefined;
    }
    throw new ApiRequestError(response.status, parseProblemDetails(problemBody), response.statusText || `Yêu cầu thất bại (${response.status}).`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

function publicRequest<T>(path: string, init?: RequestInit): Promise<T> {
  return request(`${API_BASE_URL}${path}`, {
    ...init,
    credentials: "omit",
  });
}

function privateRequest<T>(path: string, init?: RequestInit): Promise<T> {
  return request(`/api/backend${path}`, {
    ...init,
    cache: "no-store",
  }, true);
}

export async function searchRecipes(
  query: URLSearchParams,
  signal: AbortSignal,
): Promise<PaginatedResult<RecipeSummary>> {
  const params = new URLSearchParams(query);
  const search = params.get("search");
  params.delete("search");
  if (search !== null) params.set("q", search);
  return publicRequest(`/api/v1/recipes/search?${params.toString()}`, { signal });
}

export async function getRecipes(
  signal: AbortSignal,
  pageSize = 12,
  page = 1,
): Promise<PaginatedResult<RecipeSummary>> {
  return publicRequest(`/api/v1/recipes/?page=${page}&pageSize=${pageSize}`, { signal });
}

export async function getMyRecipes(
  signal: AbortSignal,
  pageSize = 12,
  page = 1,
): Promise<PaginatedResult<RecipeSummary>> {
  return privateRequest(`/recipes/mine?page=${page}&pageSize=${pageSize}`, {
    method: "GET",
    signal,
  });
}

export async function getAdminRecipes(
  signal: AbortSignal,
  pageSize = 12,
  page = 1,
): Promise<PaginatedResult<RecipeSummary>> {
  return privateRequest(`/admin/recipes?page=${page}&pageSize=${pageSize}`, {
    method: "GET",
    signal,
  });
}

export async function getPrivateRecipe(
  id: string,
  signal?: AbortSignal,
): Promise<RecipeDetail> {
  return privateRequest(`/recipes/${encodeURIComponent(id)}`, {
    method: "GET",
    signal,
  });
}

export async function getRecipe(slug: string, signal?: AbortSignal): Promise<RecipeDetail> {
  return publicRequest(`/api/v1/recipes/${encodeURIComponent(slug)}`, { signal });
}

export async function getAuthorProfile(
  id: string,
  page: number,
  signal: AbortSignal,
): Promise<AuthorProfilePage> {
  const params = new URLSearchParams({ page: String(page), pageSize: "12" });
  return publicRequest(`/api/v1/authors/${encodeURIComponent(id)}?${params.toString()}`, { signal });
}

export async function getRecipeCategories(signal: AbortSignal): Promise<RecipeCategory[]> {
  const result = await publicRequest<PaginatedResult<RecipeCategory>>(
    "/api/v1/categories/?pageSize=100",
    { signal },
  );
  return result.items;
}

export async function getCategory(slug: string, signal?: AbortSignal): Promise<RecipeCategory> {
  return publicRequest(`/api/v1/categories/${encodeURIComponent(slug)}`, { signal });
}

export async function createRecipe(payload: RecipeInput): Promise<RecipeDetail> {
  return privateRequest("/recipes", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
}

export async function updateRecipe(
  id: string,
  payload: RecipeInput & { rowVersion: string },
): Promise<RecipeDetail> {
  return privateRequest(`/recipes/${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
}

export async function deleteRecipe(id: string): Promise<void> {
  return privateRequest(`/recipes/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}

export async function publishRecipe(id: string): Promise<void> {
  return privateRequest(`/recipes/${encodeURIComponent(id)}/publish`, {
    method: "PATCH",
  });
}

export async function archiveRecipe(id: string): Promise<void> {
  return privateRequest(`/recipes/${encodeURIComponent(id)}/archive`, {
    method: "PATCH",
  });
}

export async function createCategory(
  payload: { name: string; description: string | null },
): Promise<RecipeCategory> {
  return privateRequest("/categories", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
}

export async function updateCategory(
  id: string,
  payload: { name: string; description: string | null; imageUrl: string | null; orderIndex: number },
): Promise<RecipeCategory> {
  return privateRequest(`/categories/${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
}

export async function uploadCategoryImage(
  categoryId: string,
  file: File,
): Promise<RecipeCategory> {
  const form = new FormData();
  form.append("file", file);
  return privateRequest(`/categories/${encodeURIComponent(categoryId)}/image`, {
    method: "POST",
    body: form,
  });
}

export async function deleteCategory(id: string): Promise<void> {
  return privateRequest(`/categories/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}

export interface RecipeIngredientPayload {
  name: string;
  quantity?: number | null;
  unit?: string | null;
  notes?: string | null;
  orderIndex: number;
}

export async function addRecipeIngredient(
  recipeId: string,
  payload: RecipeIngredientPayload,
): Promise<{ id: string }> {
  return privateRequest(`/recipes/${encodeURIComponent(recipeId)}/ingredients`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
}

export async function updateRecipeIngredient(
  recipeId: string,
  ingredientId: string,
  payload: RecipeIngredientPayload,
): Promise<void> {
  return privateRequest(`/recipes/${encodeURIComponent(recipeId)}/ingredients/${encodeURIComponent(ingredientId)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
}

export async function deleteRecipeIngredient(
  recipeId: string,
  ingredientId: string,
): Promise<void> {
  return privateRequest(`/recipes/${encodeURIComponent(recipeId)}/ingredients/${encodeURIComponent(ingredientId)}`, {
    method: "DELETE",
  });
}

export interface RecipeImageUploadResult {
  imageId: string;
  originalUrl: string;
  thumbnailUrl: string | null;
  mediumUrl: string | null;
  altText: string | null;
  isPrimary: boolean;
  stepNumber?: number | null;
}

export async function uploadRecipeImage(
  recipeId: string,
  file: File,
  isPrimary = false,
  altText?: string,
  stepNumber?: number,
): Promise<RecipeImageUploadResult> {
  const form = new FormData();
  form.append("file", file);
  form.append("isPrimary", String(isPrimary));
  if (altText) form.append("altText", altText);
  if (stepNumber !== undefined) form.append("stepNumber", String(stepNumber));
  return privateRequest(`/recipes/${encodeURIComponent(recipeId)}/images`, {
    method: "POST",
    body: form,
  });
}

export async function uploadRecipeStepImage(
  recipeId: string,
  stepId: string,
  file: File,
): Promise<RecipeStep> {
  const form = new FormData();
  form.append("file", file);
  return privateRequest(
    `/recipes/${encodeURIComponent(recipeId)}/steps/${encodeURIComponent(stepId)}/image`,
    {
      method: "POST",
      body: form,
    },
  );
}

export async function setPrimaryRecipeImage(
  recipeId: string,
  imageId: string,
): Promise<void> {
  return privateRequest(`/recipes/${encodeURIComponent(recipeId)}/images/${encodeURIComponent(imageId)}/primary`, {
    method: "PATCH",
  });
}

export async function deleteRecipeImage(
  recipeId: string,
  imageId: string,
): Promise<void> {
  return privateRequest(`/recipes/${encodeURIComponent(recipeId)}/images/${encodeURIComponent(imageId)}`, {
    method: "DELETE",
  });
}
