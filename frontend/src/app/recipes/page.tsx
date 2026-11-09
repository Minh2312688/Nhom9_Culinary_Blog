"use client";

import { useEffect, useState } from "react";
import { getRecipes, type PaginatedResult, type RecipeSummary } from "@/lib/recipes-api";
import { ErrorNotice, LoadingNotice, Pagination, RecipeCard, SectionTitle, SitePage } from "../site-ui";
import styles from "../site-ui.module.css";

export default function RecipesPage() {
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
    window.history.replaceState(null, "", page === 1 ? "/recipes" : `/recipes?page=${page}`);
    getRecipes(controller.signal, 12, undefined, page).then(setResult).catch((reason: unknown) => {
      if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Không thể tải công thức.");
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, [initialized, page]);

  return (
    <SitePage>
      <main className={styles.content}>
        <SectionTitle eyebrow="THƯ VIỆN MÓN NGON" title="Tất cả công thức" description="Tìm cảm hứng cho bữa ăn tiếp theo của bạn." />
        {loading && <LoadingNotice />}
        {error && <ErrorNotice>{error}</ErrorNotice>}
        {!loading && !error && result?.items.length === 0 && <p className={styles.notice}>Chưa có công thức nào.</p>}
        <div className={styles.grid}>
          {result?.items.map((recipe) => (
            <RecipeCard recipe={recipe} key={recipe.id} />
          ))}
        </div>
        {result && <Pagination page={result.page} totalPages={result.totalPages}
          hasNextPage={result.hasNextPage} hasPreviousPage={result.hasPreviousPage}
          onPageChange={setPage} label="Phân trang công thức" />}
      </main>
    </SitePage>
  );
}
