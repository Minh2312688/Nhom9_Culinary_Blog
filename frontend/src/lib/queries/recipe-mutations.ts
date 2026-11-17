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
import { queryKeys } from "./query-keys";

async function invalidateRecipeQueries(
  userId: string,
  recipeId: string,
  queryClient: ReturnType<typeof useQueryClient>,
  options: { recipe?: RecipeDetail; slug?: string; includePublic?: boolean } = {},
) {
  const detailKey = queryKeys.dashboard.recipeDetail(userId, recipeId);
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

export function useCreateRecipe(userId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createRecipe,
    onSuccess: (recipe) => invalidateRecipeQueries(userId, recipe.id, queryClient, { recipe }),
  });
}

export function useUpdateRecipe(userId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: RecipeInput & { rowVersion: string } }) =>
      updateRecipe(id, payload),
    onSuccess: (recipe) => invalidateRecipeQueries(userId, recipe.id, queryClient, { recipe }),
  });
}

export function useDeleteRecipe(userId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id }: { id: string; slug?: string }) => deleteRecipe(id),
    onSuccess: (_result, { id, slug }) =>
      invalidateRecipeQueries(userId, id, queryClient, { slug, includePublic: true }),
  });
}

export function usePublishRecipe(userId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id }: { id: string; slug?: string }) => publishRecipe(id),
    onSuccess: (_result, { id, slug }) =>
      invalidateRecipeQueries(userId, id, queryClient, { slug, includePublic: true }),
  });
}

export function useArchiveRecipe(userId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id }: { id: string; slug?: string }) => archiveRecipe(id),
    onSuccess: (_result, { id, slug }) =>
      invalidateRecipeQueries(userId, id, queryClient, { slug, includePublic: true }),
  });
}

export function useUploadRecipeImage(userId: string) {
  const queryClient = useQueryClient();
  return useMutation<RecipeImageUploadResult, Error, {
    recipeId: string;
    file: File;
    isPrimary?: boolean;
    altText?: string;
  }>({
    mutationFn: ({ recipeId, file, isPrimary, altText }) =>
      uploadRecipeImage(recipeId, file, isPrimary, altText),
    onSuccess: (_result, { recipeId }) =>
      invalidateRecipeQueries(userId, recipeId, queryClient),
  });
}

export function useUploadRecipeStepImage(userId: string) {
  const queryClient = useQueryClient();
  return useMutation<RecipeStep, Error, { recipeId: string; stepId: string; file: File }>({
    mutationFn: ({ recipeId, stepId, file }) =>
      uploadRecipeStepImage(recipeId, stepId, file),
    onSuccess: (_result, { recipeId }) =>
      invalidateRecipeQueries(userId, recipeId, queryClient),
  });
}

export function useSetPrimaryRecipeImage(userId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ recipeId, imageId }: { recipeId: string; imageId: string }) =>
      setPrimaryRecipeImage(recipeId, imageId),
    onSuccess: (_result, { recipeId }) =>
      invalidateRecipeQueries(userId, recipeId, queryClient),
  });
}

export function useDeleteRecipeImage(userId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ recipeId, imageId }: { recipeId: string; imageId: string }) =>
      deleteRecipeImage(recipeId, imageId),
    onSuccess: (_result, { recipeId }) =>
      invalidateRecipeQueries(userId, recipeId, queryClient),
  });
}
