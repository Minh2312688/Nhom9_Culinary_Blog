"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { getRecipeCategories, type RecipeCategory } from "@/lib/recipes-api";
import { ErrorNotice, LoadingNotice, SectionTitle, SitePage } from "../site-ui";
import styles from "../site-ui.module.css";

export default function CategoriesPage() {
  const [categories, setCategories] = useState<RecipeCategory[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    getRecipeCategories(controller.signal).then(setCategories).catch((reason: unknown) => {
      if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Không thể tải danh mục.");
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, []);

  return (
    <SitePage>
      <main className={styles.content}>
        <SectionTitle eyebrow="KHÁM PHÁ THEO CHỦ ĐỀ" title="Danh mục món ăn" description="Chọn một chủ đề để tìm công thức phù hợp với khẩu vị." />
        {loading && <LoadingNotice />}
        {error && <ErrorNotice>{error}</ErrorNotice>}
        {!loading && !error && categories.length === 0 && <p className={styles.notice}>Chưa có danh mục.</p>}
        <div className={styles.grid}>
          {categories.map((category) => (
            <Link className={styles.card} href={`/categories/${category.slug}`} key={category.id}>
              <p className={styles.cardLabel}>DANH MỤC</p>
              <h2>{category.name}</h2>
              {category.description && <p className={styles.cardText}>{category.description}</p>}
              <div className={styles.cardMeta}><span>{category.recipeCount ?? 0} công thức</span></div>
            </Link>
          ))}
        </div>
      </main>
    </SitePage>
  );
}
