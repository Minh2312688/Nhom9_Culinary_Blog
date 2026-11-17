"use client";

import { useEffect, useState } from "react";
import { useRecipes } from "@/lib/queries/recipes";
import { ErrorNotice, LoadingNotice, Pagination, RecipeCard, SectionTitle, SitePage } from "../site-ui";
import styles from "../site-ui.module.css";

export default function RecipesPage() {
  const [page, setPage] = useState(1);
  const [initialized, setInitialized] = useState(false);
  const recipesQuery = useRecipes({ page, pageSize: 12, enabled: initialized });
  const result = recipesQuery.data;
  const loading = !initialized || recipesQuery.isFetching;

  useEffect(() => {
    const requestedPage = Number(new URLSearchParams(window.location.search).get("page"));
    setPage(Number.isSafeInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1);
    setInitialized(true);
  }, []);

  useEffect(() => {
    if (!initialized) return;
    window.history.replaceState(null, "", page === 1 ? "/recipes" : `/recipes?page=${page}`);
  }, [initialized, page]);

  return (
    <SitePage>
      <main className={styles.content}>
        <SectionTitle eyebrow="THƯ VIỆN MÓN NGON" title="Tất cả công thức" description="Tìm cảm hứng cho bữa ăn tiếp theo của bạn." />
        {loading && <LoadingNotice />}
        {recipesQuery.error && <ErrorNotice>{recipesQuery.error.message || "Không thể tải công thức."}</ErrorNotice>}
        {!loading && !recipesQuery.error && result?.items.length === 0 && <p className={styles.notice}>Chưa có công thức nào.</p>}
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
