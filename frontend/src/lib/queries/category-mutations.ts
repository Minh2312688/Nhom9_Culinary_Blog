import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  createCategory,
  deleteCategory,
  updateCategory,
  uploadCategoryImage,
} from "@/lib/recipes-api";
import { queryKeys } from "./query-keys";

async function invalidateCategoryQueries(
  userId: string,
  queryClient: ReturnType<typeof useQueryClient>,
) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.categories(userId) }),
    queryClient.invalidateQueries({ queryKey: queryKeys.categories.all }),
    queryClient.invalidateQueries({ queryKey: queryKeys.recipes.lists() }),
    queryClient.invalidateQueries({ queryKey: queryKeys.search.all }),
  ]);
}

export function useCreateCategory(userId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createCategory,
    onSuccess: () => invalidateCategoryQueries(userId, queryClient),
  });
}

export function useUpdateCategory(userId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: {
      id: string;
      payload: { name: string; description: string | null; imageUrl: string | null; orderIndex: number };
    }) => updateCategory(id, payload),
    onSuccess: () => invalidateCategoryQueries(userId, queryClient),
  });
}

export function useDeleteCategory(userId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deleteCategory(id),
    onSuccess: () => invalidateCategoryQueries(userId, queryClient),
  });
}

export function useUploadCategoryImage(userId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ categoryId, file }: { categoryId: string; file: File }) =>
      uploadCategoryImage(categoryId, file),
    onSuccess: () => invalidateCategoryQueries(userId, queryClient),
  });
}
