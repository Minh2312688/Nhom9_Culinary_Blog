export const queryKeys = {
  recipes: {
    all: ["recipes"] as const,
    lists: () => [...queryKeys.recipes.all, "list"] as const,
    list: (params: { page: number; pageSize: number }) =>
      [...queryKeys.recipes.lists(), params] as const,
    details: () => [...queryKeys.recipes.all, "detail"] as const,
    detail: (slug: string) => [...queryKeys.recipes.details(), slug] as const,
  },
  categories: {
    all: ["categories"] as const,
    list: () => [...queryKeys.categories.all, "list"] as const,
    detail: (slug: string) => [...queryKeys.categories.all, "detail", slug] as const,
    recipes: (slug: string, page: number) =>
      [...queryKeys.categories.all, "recipes", slug, page] as const,
  },
  search: {
    all: ["search"] as const,
    recipes: (params: string) => [...queryKeys.search.all, "recipes", params] as const,
  },
  dashboard: {
    recipeLists: (userId: string) => ["dashboard", userId, "recipes"] as const,
    recipes: (userId: string, page: number, scope: "mine" | "all" = "mine") =>
      [...queryKeys.dashboard.recipeLists(userId), page, scope] as const,
    recipeDetails: (userId: string) => ["dashboard", userId, "recipe-detail"] as const,
    recipeDetail: (userId: string, id: string) =>
      [...queryKeys.dashboard.recipeDetails(userId), id] as const,
    categories: (userId: string) => ["dashboard", userId, "categories"] as const,
  },
  profile: {
    detail: (userId: string) => ["profile", userId] as const,
  },
  authors: {
    profile: (id: string, page: number) => ["authors", id, page] as const,
  },
};
