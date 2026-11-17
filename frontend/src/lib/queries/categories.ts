import { keepPreviousData, useQuery } from "@tanstack/react-query";
import {
  getCategory,
  getRecipeCategories,
  searchRecipes,
  type RecipeCategory,
} from "@/lib/recipes-api";
import { queryKeys } from "./query-keys";

export function useCategories(enabled = true) {
  return useQuery({
    queryKey: queryKeys.categories.list(),
    queryFn: ({ signal }) => getRecipeCategories(signal),
    enabled,
  });
}

export function useCategory(slug: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.categories.detail(slug),
    queryFn: ({ signal }) => getCategory(slug, signal),
    enabled: enabled && Boolean(slug),
  });
}

export function useCategoryRecipes(
  slug: string,
  categoryId: string | undefined,
  page: number,
  enabled = true,
) {
  return useQuery({
    queryKey: queryKeys.categories.recipes(slug, page),
    queryFn: ({ signal }) => {
      if (!categoryId) {
        throw new Error("Không thể xác định danh mục.");
      }

      return searchRecipes(
        new URLSearchParams({
          categoryId,
          page: String(page),
          pageSize: "12",
        }),
        signal,
      );
    },
    enabled: enabled && Boolean(slug && categoryId),
    placeholderData: keepPreviousData,
  });
}

export function useDashboardCategories(userId: string, enabled = true) {
  return useQuery<RecipeCategory[]>({
    queryKey: queryKeys.dashboard.categories(userId),
    queryFn: ({ signal }) => getRecipeCategories(signal),
    enabled: enabled && Boolean(userId),
  });
}
