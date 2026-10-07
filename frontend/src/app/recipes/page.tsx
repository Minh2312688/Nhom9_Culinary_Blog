"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { getRecipes, type RecipeSummary } from "@/lib/recipes-api";
import { ErrorNotice, LoadingNotice, SectionTitle, SitePage } from "../site-ui";
import styles from "../site-ui.module.css";

export default function RecipesPage() {
  const [recipes, setRecipes] = useState<RecipeSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    getRecipes(controller.signal, 50).then(({ items }) => setRecipes(items)).catch((reason: unknown) => {
      if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Không thể tải công thức.");
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, []);

  return (
    <SitePage>
      <main className={styles.content}>
        <SectionTitle eyebrow="THƯ VIỆN MÓN NGON" title="Tất cả công thức" description="Tìm cảm hứng cho bữa ăn tiếp theo của bạn." />
        {loading && <LoadingNotice />}
        {error && <ErrorNotice>{error}</ErrorNotice>}
        {!loading && !error && recipes.length === 0 && <p className={styles.notice}>Chưa có công thức nào.</p>}
        <div className={styles.grid}>
          {recipes.map((recipe) => (
            <Link className={styles.card} href={`/recipes/${recipe.slug}`} key={recipe.id}>
              <p className={styles.cardLabel}>{recipe.difficulty}</p>
              <h2>{recipe.title}</h2>
              {recipe.description && <p className={styles.cardText}>{recipe.description}</p>}
              <div className={styles.cardMeta}><span>{recipe.cookTimeMinutes} phút</span><span>{recipe.servings} khẩu phần</span></div>
            </Link>
          ))}
        </div>
      </main>
    </SitePage>
  );
}
