"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { getCategory, searchRecipes, type RecipeCategory, type RecipeSummary } from "@/lib/recipes-api";
import { ErrorNotice, LoadingNotice, SitePage } from "../../site-ui";
import styles from "../../site-ui.module.css";

export default function CategoryDetailPage() {
  const { slug } = useParams<{ slug: string }>();
  const [category, setCategory] = useState<RecipeCategory | null>(null);
  const [recipes, setRecipes] = useState<RecipeSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    async function load() {
      try {
        const item = await getCategory(slug, controller.signal);
        setCategory(item);
        const query = new URLSearchParams({ categoryId: item.id, page: "1", pageSize: "50" });
        const result = await searchRecipes(query, controller.signal);
        setRecipes(result.items);
      } catch (reason) {
        if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Không thể tải danh mục.");
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    }
    void load();
    return () => controller.abort();
  }, [slug]);

  return (
    <SitePage>
      <main className={styles.content}>
        <p className={styles.eyebrow}><Link href="/categories">DANH MỤC</Link> / CHI TIẾT</p>
        {loading && <LoadingNotice />}
        {error && <ErrorNotice>{error}</ErrorNotice>}
        {category && <>
          <div className={styles.sectionTitle}>
            <h1>{category.name}</h1>
            {category.description && <p className={styles.description}>{category.description}</p>}
          </div>
          <div className={styles.grid}>
            {recipes.map((recipe) => (
              <Link className={styles.card} href={`/recipes/${recipe.slug}`} key={recipe.id}>
                <p className={styles.cardLabel}>{recipe.difficulty}</p>
                <h2>{recipe.title}</h2>
                {recipe.description && <p className={styles.cardText}>{recipe.description}</p>}
                <div className={styles.cardMeta}><span>{recipe.cookTimeMinutes} phút</span></div>
              </Link>
            ))}
          </div>
          {!loading && recipes.length === 0 && <p className={styles.notice}>Danh mục này chưa có công thức.</p>}
        </>}
      </main>
    </SitePage>
  );
}
