"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { getRecipe, type RecipeDetail } from "@/lib/recipes-api";
import { ErrorNotice, LoadingNotice, SitePage } from "../../site-ui";
import styles from "../../site-ui.module.css";

export default function RecipeDetailPage() {
  const { slug } = useParams<{ slug: string }>();
  const [recipe, setRecipe] = useState<RecipeDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    getRecipe(slug, controller.signal).then(setRecipe).catch((reason: unknown) => {
      if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Không thể tải công thức.");
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, [slug]);

  return (
    <SitePage>
      <main className={styles.detail}>
        <p className={styles.eyebrow}><Link href="/recipes">CÔNG THỨC</Link> / CHI TIẾT</p>
        {loading && <LoadingNotice />}
        {error && <ErrorNotice>{error}</ErrorNotice>}
        {recipe && <>
          <h1>{recipe.title}</h1>
          {recipe.description && <p className={styles.detailLead}>{recipe.description}</p>}
          <div className={styles.cardMeta}>
            <span>Chuẩn bị {recipe.prepTimeMinutes} phút</span>
            <span>Nấu {recipe.cookTimeMinutes} phút</span>
            <span>{recipe.servings} khẩu phần</span>
            <span>{recipe.difficulty}</span>
          </div>
          <section className={styles.detailSection}>
            <h2>Nguyên liệu</h2>
            {recipe.ingredients.length ? <ul className={styles.ingredientList}>
              {recipe.ingredients.map((ingredient, index) => (
                <li key={ingredient.id ?? `${ingredient.name}-${index}`}>
                  {[ingredient.quantity, ingredient.unit, ingredient.name].filter(Boolean).join(" ")}
                  {ingredient.notes && ` (${ingredient.notes})`}
                </li>
              ))}
            </ul> : <p className={styles.notice}>Chưa có nguyên liệu.</p>}
          </section>
          <section className={styles.detailSection}>
            <h2>Cách thực hiện</h2>
            {recipe.steps.length ? <ol className={styles.stepList}>
              {recipe.steps.map((step, index) => <li key={step.id ?? index}>
                <strong>{step.title || `Bước ${step.stepNumber ?? index + 1}`}</strong>
                <div>{step.description}</div>
              </li>)}
            </ol> : <p className={styles.notice}>Chưa có hướng dẫn chế biến.</p>}
          </section>
        </>}
      </main>
    </SitePage>
  );
}
