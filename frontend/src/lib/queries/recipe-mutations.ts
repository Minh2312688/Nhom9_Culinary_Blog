import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  archiveRecipe,
  createRecipe,
  deleteRecipe,
  deleteRecipeImage,
  publishRecipe,
  setPrimaryRecipeImage,
  updateRecipe,
  uploadRecipeImage,
  uploadRecipeStepImage,
  type RecipeDetail,
  type RecipeImageUploadResult,
  type RecipeInput,
  type RecipeStep,
} from "@/lib/recipes-api";
import { queryKeys, type RecipeAccessScope } from "./query-keys";

async function invalidateRecipeQueries(
  userId: string,
  recipeId: string,
  queryClient: ReturnType<typeof useQueryClient>,
  accessScope: RecipeAccessScope,
  options: { recipe?: RecipeDetail; slug?: string; includePublic?: boolean } = {},
) {
  const detailKey = queryKeys.dashboard.recipeDetail(userId, recipeId, accessScope);
  if (options.recipe) queryClient.setQueryData(detailKey, options.recipe);
  const recipe = options.recipe ?? queryClient.getQueryData<RecipeDetail>(detailKey);
  const isPublic = options.includePublic
    || recipe?.status === 1
    || recipe?.status === "Published";

  const invalidations = [
    queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.recipeLists(userId) }),
    queryClient.invalidateQueries({ queryKey: detailKey }),
  ];

  if (isPublic) {
    invalidations.push(
      queryClient.invalidateQueries({ queryKey: queryKeys.recipes.lists() }),
      queryClient.invalidateQueries({ queryKey: queryKeys.search.all }),
    );
    const slug = options.slug ?? recipe?.slug;
    if (slug) invalidations.push(
      queryClient.invalidateQueries({ queryKey: queryKeys.recipes.detail(slug) }),
    );
  }

  await Promise.all(invalidations);
}

export function useCreateRecipe(userId: string, accessScope: RecipeAccessScope = "owner") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createRecipe,
    onSuccess: (recipe) =>
      invalidateRecipeQueries(userId, recipe.id, queryClient, accessScope, { recipe }),
  });
}

export function useUpdateRecipe(userId: string, accessScope: RecipeAccessScope = "owner") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: RecipeInput & { rowVersion: string } }) =>
      updateRecipe(id, payload),
    onSuccess: (recipe) =>
      invalidateRecipeQueries(userId, recipe.id, queryClient, accessScope, { recipe }),
  });
}

export function useDeleteRecipe(userId: string, accessScope: RecipeAccessScope = "owner") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id }: { id: string; slug?: string }) => deleteRecipe(id),
    onSuccess: (_result, { id, slug }) =>
      invalidateRecipeQueries(userId, id, queryClient, accessScope, { slug, includePublic: true }),
  });
}

export function usePublishRecipe(userId: string, accessScope: RecipeAccessScope = "owner") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id }: { id: string; slug?: string }) => publishRecipe(id),
    onSuccess: (_result, { id, slug }) =>
      invalidateRecipeQueries(userId, id, queryClient, accessScope, { slug, includePublic: true }),
  });
}

export function useArchiveRecipe(userId: string, accessScope: RecipeAccessScope = "owner") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id }: { id: string; slug?: string }) => archiveRecipe(id),
    onSuccess: (_result, { id, slug }) =>
      invalidateRecipeQueries(userId, id, queryClient, accessScope, { slug, includePublic: true }),
  });
}

export function useUploadRecipeImage(userId: string, accessScope: RecipeAccessScope = "owner") {
  const queryClient = useQueryClient();
  return useMutation<RecipeImageUploadResult, Error, {
    recipeId: string;
    file: File;
    isPrimary?: boolean;
    altText?: string;
    stepNumber?: number;
  }>({
    mutationFn: ({ recipeId, file, isPrimary, altText, stepNumber }) =>
      uploadRecipeImage(recipeId, file, isPrimary, altText, stepNumber),
    onSuccess: (_result, { recipeId }) =>
      invalidateRecipeQueries(userId, recipeId, queryClient, accessScope),
  });
}

export function useUploadRecipeStepImage(userId: string, accessScope: RecipeAccessScope = "owner") {
  const queryClient = useQueryClient();
  return useMutation<RecipeStep, Error, { recipeId: string; stepId: string; file: File }>({
    mutationFn: ({ recipeId, stepId, file }) =>
      uploadRecipeStepImage(recipeId, stepId, file),
    onSuccess: (_result, { recipeId }) =>
      invalidateRecipeQueries(userId, recipeId, queryClient, accessScope),
  });
}

export function useSetPrimaryRecipeImage(userId: string, accessScope: RecipeAccessScope = "owner") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ recipeId, imageId }: { recipeId: string; imageId: string }) =>
      setPrimaryRecipeImage(recipeId, imageId),
    onSuccess: (_result, { recipeId }) =>
      invalidateRecipeQueries(userId, recipeId, queryClient, accessScope),
  });
}

export function useDeleteRecipeImage(userId: string, accessScope: RecipeAccessScope = "owner") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ recipeId, imageId }: { recipeId: string; imageId: string }) =>
      deleteRecipeImage(recipeId, imageId),
    onSuccess: (_result, { recipeId }) =>
      invalidateRecipeQueries(userId, recipeId, queryClient, accessScope),
  });
}
