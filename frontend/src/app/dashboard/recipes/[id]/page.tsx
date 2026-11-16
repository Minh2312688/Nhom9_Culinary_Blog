"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { ApiRequestError, getPrivateRecipe, type RecipeDetail } from "@/lib/recipes-api";
import { RecipeDetailClient, RecipeDetailError } from "@/app/recipes/[slug]/recipe-detail-client";
import { LoadingSkeleton } from "@/app/site-ui";

export default function DashboardRecipeViewPage() {
  const params = useParams<{ id: string }>();
  const id = params?.id;
  const [recipe, setRecipe] = useState<RecipeDetail | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) {
      setError("Không thể xác định công thức cần xem.");
      return;
    }

    const controller = new AbortController();
    getPrivateRecipe(id, controller.signal)
      .then(setRecipe)
      .catch((reason: unknown) => {
        if (controller.signal.aborted) return;
        setError(reason instanceof ApiRequestError && reason.status === 403
          ? "Bạn không có quyền xem công thức này."
          : reason instanceof Error ? reason.message : "Không thể tải công thức.");
      });

    return () => controller.abort();
  }, [id]);

  if (error) return <RecipeDetailError message={error} />;
  if (!recipe) return <LoadingSkeleton variant="cards" />;

  return (
    <RecipeDetailClient
      recipe={recipe}
      backHref="/dashboard/recipes"
      backLabel="DANH SÁCH CÔNG THỨC"
    />
  );
}
