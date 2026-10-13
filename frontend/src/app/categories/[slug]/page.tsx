"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { getCategory, searchRecipes, type PaginatedResult, type RecipeCategory, type RecipeSummary } from "@/lib/recipes-api";
import { ErrorNotice, LoadingNotice, Pagination, SitePage } from "../../site-ui";
import styles from "../../site-ui.module.css";

export default function CategoryDetailPage() {
  const { slug } = useParams<{ slug: string }>();
  const [category, setCategory] = useState<RecipeCategory | null>(null);
  const [result, setResult] = useState<PaginatedResult<RecipeSummary> | null>(null);
  const [page, setPage] = useState(1);
  const [initialized, setInitialized] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const requestedPage = Number(new URLSearchParams(window.location.search).get("page"));
    setPage(Number.isSafeInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1);
    setInitialized(true);
  }, []);

  useEffect(() => {
    if (!initialized) return;
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    window.history.replaceState(null, "", page === 1 ? `/categories/${slug}` : `/categories/${slug}?page=${page}`);
    async function load() {
      try {
        const item = await getCategory(slug, controller.signal);
        setCategory(item);
        const query = new URLSearchParams({ categoryId: item.id, page: String(page), pageSize: "12" });
        setResult(await searchRecipes(query, controller.signal));
      } catch (reason) {
        if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Không thể tải danh mục.");
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    }
    void load();
    return () => controller.abort();
  }, [initialized, page, slug]);

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
            {result?.items.map((recipe) => (
              <Link className={styles.card} href={`/recipes/${recipe.slug}`} key={recipe.id}>
                <p className={styles.cardLabel}>{recipe.difficulty}</p>
                <h2>{recipe.title}</h2>
                {recipe.description && <p className={styles.cardText}>{recipe.description}</p>}
                <div className={styles.cardMeta}><span>{recipe.cookTimeMinutes} phút</span></div>
              </Link>
            ))}
          </div>
          {!loading && result?.items.length === 0 && <p className={styles.notice}>Danh mục này chưa có công thức.</p>}
          {result && <Pagination page={result.page} totalPages={result.totalPages}
            hasNextPage={result.hasNextPage} hasPreviousPage={result.hasPreviousPage}
            onPageChange={setPage} label="Phân trang công thức trong danh mục" />}
        </>}
      </main>
    </SitePage>
  );
}
