"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { deleteRecipe, getMyRecipes, publishRecipe, type PaginatedResult, type RecipeSummary } from "@/lib/recipes-api";
import { useFeedback } from "../../feedback";
import { ErrorNotice, LoadingSkeleton, Pagination, SectionTitle } from "../../site-ui";
import styles from "../../site-ui.module.css";

export default function DashboardRecipesPage() {
  const { showToast } = useFeedback();
  const [result, setResult] = useState<PaginatedResult<RecipeSummary> | null>(null);
  const [page, setPage] = useState(1);
  const [initialized, setInitialized] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadRecipes = useCallback(async (requestedPage = page) => {
    setLoading(true);
    setError(null);
    try {
      setResult(await getMyRecipes(new AbortController().signal, 12, requestedPage));
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Không thể tải công thức.";
      setError(message);
      showToast(message, "error");
    } finally {
      setLoading(false);
    }
  }, [page, showToast]);

  useEffect(() => {
    const requestedPage = Number(new URLSearchParams(window.location.search).get("page"));
    setPage(Number.isSafeInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1);
    setInitialized(true);
  }, []);

  useEffect(() => {
    if (!initialized) return;
    window.history.replaceState(null, "", page === 1 ? "/dashboard/recipes" : `/dashboard/recipes?page=${page}`);
    void loadRecipes(page);
  }, [initialized, loadRecipes, page]);

  async function handlePublish(id: string) {
    try {
      await publishRecipe(id);
      showToast("Đã xuất bản công thức.", "success");
      await loadRecipes();
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Không thể xuất bản công thức.";
      setError(message);
      showToast(message, "error");
    }
  }

  async function handleDelete(id: string) {
    if (!window.confirm("Bạn có chắc muốn xóa công thức này?")) return;
    try {
      await deleteRecipe(id);
      showToast("Đã xóa công thức.", "success");
      await loadRecipes();
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Không thể xóa công thức.";
      setError(message);
      showToast(message, "error");
    }
  }

  function statusLabel(status: string | number) {
    if (status === 0 || status === "Draft") return "Bản nháp";
    if (status === 1 || status === "Published") return "Đã xuất bản";
    if (status === 2 || status === "Archived") return "Đã lưu trữ";
    return String(status);
  }

  return (
      <main className={styles.content}>
        <SectionTitle eyebrow="QUẢN LÝ NỘI DUNG" title="Công thức của tôi" description="Lưu bản nháp, cập nhật hoặc xuất bản nội dung." />
        <div className={styles.toolbar}>
          <h2>{result?.totalCount ?? 0} công thức</h2>
          <Link className={styles.button} href="/dashboard/recipes/new">Tạo công thức</Link>
        </div>
        {loading && <LoadingSkeleton variant="cards" />}
        {error && <ErrorNotice><span data-error-summary>{error}</span></ErrorNotice>}
        {!loading && !error && result?.items.length === 0 && (
          <section className={`${styles.notice} ${styles.emptyState}`} aria-label="Chưa có công thức">
            <p>Bạn chưa có công thức nào.</p>
          </section>
        )}
        {!!result?.items.length && <div style={{ overflowX: "auto" }}>
          <table className={styles.table}>
            <thead><tr><th>Tên món</th><th>Trạng thái</th><th>Thời gian nấu</th><th>Thao tác</th></tr></thead>
            <tbody>{result.items.map((recipe) => (
              <tr key={recipe.id}>
                <td><strong>{recipe.title}</strong></td>
                <td><span className={styles.status}>{statusLabel(recipe.status)}</span></td>
                <td>{recipe.cookTimeMinutes} phút</td>
                <td>
                  <div className={`${styles.actions} ${styles.tableActions}`}>
                    <Link href={`/dashboard/recipes/${recipe.id}/edit`}>Sửa</Link>
                    {(recipe.status === 0 || recipe.status === "Draft") &&
                      <button type="button" onClick={() => void handlePublish(recipe.id)}>Xuất bản</button>}
                    <button type="button" onClick={() => void handleDelete(recipe.id)}>Xóa</button>
                  </div>
                </td>
              </tr>
            ))}</tbody>
          </table>
        </div>}
        {result && <Pagination page={result.page} totalPages={result.totalPages}
          hasNextPage={result.hasNextPage} hasPreviousPage={result.hasPreviousPage}
          onPageChange={setPage} label="Phân trang công thức của tôi" />}
      </main>
  );
}
