import { keepPreviousData, useQuery } from "@tanstack/react-query";
import {
  getMyRecipes,
  getAdminRecipes,
  getPrivateRecipe,
  getRecipe,
  getRecipes,
  searchRecipes,
  type RecipeDetail,
} from "@/lib/recipes-api";
import { queryKeys, type RecipeAccessScope } from "./query-keys";

export function useRecipes(
  params: { page: number; pageSize: number; enabled?: boolean },
) {
  return useQuery({
    queryKey: queryKeys.recipes.list({ page: params.page, pageSize: params.pageSize }),
    queryFn: ({ signal }) => getRecipes(signal, params.pageSize, params.page),
    enabled: params.enabled,
    placeholderData: keepPreviousData,
  });
}

export function useSearchRecipes(params: URLSearchParams, enabled = true) {
  const canonicalParams = new URLSearchParams(params);
  canonicalParams.sort();
  const canonicalQuery = canonicalParams.toString();

  return useQuery({
    queryKey: queryKeys.search.recipes(canonicalQuery),
    queryFn: ({ signal }) => searchRecipes(new URLSearchParams(canonicalQuery), signal),
    enabled,
    placeholderData: keepPreviousData,
  });
}

export function useRecipe(slug: string, initialData: RecipeDetail) {
  return useQuery({
    queryKey: queryKeys.recipes.detail(slug),
    queryFn: ({ signal }) => getRecipe(slug, signal),
    initialData,
  });
}

export function useDashboardRecipes(
  userId: string,
  page: number,
  scope: "mine" | "all",
  enabled = true,
) {
  return useQuery({
    queryKey: queryKeys.dashboard.recipes(userId, page, scope),
    queryFn: ({ signal }) => scope === "all"
      ? getAdminRecipes(signal, 12, page)
      : getMyRecipes(signal, 12, page),
    enabled: enabled && Boolean(userId),
    placeholderData: keepPreviousData,
  });
}

export function useMyRecipes(userId: string, page: number, enabled = true) {
  return useDashboardRecipes(userId, page, "mine", enabled);
}

export function useRecipeForEdit(
  userId: string,
  id: string,
  enabled = true,
  accessScope: RecipeAccessScope = "owner",
) {
  return useQuery({
    queryKey: queryKeys.dashboard.recipeDetail(userId, id, accessScope),
    queryFn: ({ signal }) => getPrivateRecipe(id, signal),
    enabled: enabled && Boolean(id),
  });
}
