"use client";

import { useSession } from "next-auth/react";
import { useParams } from "next/navigation";
import { ApiRequestError } from "@/lib/recipes-api";
import { useRecipeForEdit } from "@/lib/queries/recipes";
import { RecipeDetailClient, RecipeDetailError } from "@/app/recipes/[slug]/recipe-detail-client";
import { LoadingSkeleton } from "@/app/site-ui";

export default function DashboardRecipeViewPage() {
  const params = useParams<{ id: string }>();
  const id = params?.id;
  const { data: session, status: sessionStatus } = useSession();
  const recipeQuery = useRecipeForEdit(
    session?.user.id ?? "",
    id ?? "",
    Boolean(id) && sessionStatus !== "loading",
  );
  const recipe = recipeQuery.data;
  const error = !id
    ? "Không thể xác định công thức cần xem."
    : recipeQuery.error instanceof ApiRequestError && recipeQuery.error.status === 403
      ? "Bạn không có quyền xem công thức này."
      : recipeQuery.error instanceof Error
        ? recipeQuery.error.message
        : recipeQuery.error ? "Không thể tải công thức." : null;

  if (error) return <RecipeDetailError message={error} />;
  if (sessionStatus === "loading" || recipeQuery.isPending || !recipe) {
    return <LoadingSkeleton variant="cards" />;
  }

  return (
    <RecipeDetailClient
      recipe={recipe}
      backHref="/dashboard/recipes"
      backLabel="DANH SÁCH CÔNG THỨC"
    />
  );
}
