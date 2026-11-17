"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { useCategory, useCategoryRecipes } from "@/lib/queries/categories";
import { ErrorNotice, LoadingNotice, Pagination, RecipeCard, SitePage } from "../../site-ui";
import styles from "../../site-ui.module.css";

export default function CategoryDetailPage() {
  const params = useParams<{ slug: string }>();
  const slug = params?.slug;
  const [page, setPage] = useState(1);
  const [initialized, setInitialized] = useState(false);
  const categoryQuery = useCategory(slug ?? "", initialized);
  const category = categoryQuery.data ?? null;
  const recipesQuery = useCategoryRecipes(slug ?? "", category?.id, page, initialized);
  const result = recipesQuery.data;
  const loading = !initialized || categoryQuery.isFetching || recipesQuery.isFetching;
  const error = categoryQuery.error?.message ?? recipesQuery.error?.message ?? null;

  useEffect(() => {
    const requestedPage = Number(new URLSearchParams(window.location.search).get("page"));
    setPage(Number.isSafeInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1);
    setInitialized(true);
  }, []);

  useEffect(() => {
    if (!initialized) return;
    const categorySlug = slug ?? "";
    window.history.replaceState(null, "", page === 1 ? `/categories/${categorySlug}` : `/categories/${categorySlug}?page=${page}`);
  }, [initialized, page, slug]);

  const visibleError = slug ? error : "Không thể xác định danh mục.";

  return (
    <SitePage>
      <main className={styles.content}>
        <p className={styles.eyebrow}><Link href="/categories">DANH MỤC</Link> / CHI TIẾT</p>
        {loading && <LoadingNotice />}
        {visibleError && <ErrorNotice>{visibleError}</ErrorNotice>}
        {category && <>
          <div className={styles.sectionTitle}>
            <h1>{category.name}</h1>
            {category.description && <p className={styles.description}>{category.description}</p>}
          </div>
          <div className={styles.grid}>
            {result?.items.map((recipe) => (
              <RecipeCard recipe={recipe} key={recipe.id} />
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
