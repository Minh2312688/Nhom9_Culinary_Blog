"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { deleteRecipe, getRecipes, publishRecipe, type PaginatedResult, type RecipeSummary } from "@/lib/recipes-api";
import { ErrorNotice, LoadingNotice, Pagination, SectionTitle, SitePage } from "../../site-ui";
import styles from "../../site-ui.module.css";

export default function DashboardRecipesPage() {
  const router = useRouter();
  const [token, setToken] = useState("");
  const [result, setResult] = useState<PaginatedResult<RecipeSummary> | null>(null);
  const [page, setPage] = useState(1);
  const [initialized, setInitialized] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadRecipes = useCallback(async (accessToken: string, requestedPage = page) => {
    setLoading(true);
    setError(null);
    try {
      setResult(await getRecipes(new AbortController().signal, 12, accessToken, requestedPage));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Không thể tải công thức.");
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => {
    const requestedPage = Number(new URLSearchParams(window.location.search).get("page"));
    setPage(Number.isSafeInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1);
    setInitialized(true);
  }, []);

  useEffect(() => {
    if (!initialized) return;
    const accessToken = sessionStorage.getItem("culinary_access_token");
    if (!accessToken) {
      router.replace("/auth/login");
      return;
    }
    setToken(accessToken);
    window.history.replaceState(null, "", page === 1 ? "/dashboard/recipes" : `/dashboard/recipes?page=${page}`);
    void loadRecipes(accessToken);
  }, [initialized, loadRecipes, page, router]);

  async function handlePublish(id: string) {
    try {
      await publishRecipe(token, id);
      await loadRecipes(token);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Không thể xuất bản công thức.");
    }
  }

  async function handleDelete(id: string) {
    if (!window.confirm("Bạn có chắc muốn xóa công thức này?")) return;
    try {
      await deleteRecipe(token, id);
      await loadRecipes(token);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Không thể xóa công thức.");
    }
  }

  function statusLabel(status: string | number) {
    if (status === 0 || status === "Draft") return "Bản nháp";
    if (status === 1 || status === "Published") return "Đã xuất bản";
    if (status === 2 || status === "Archived") return "Đã lưu trữ";
    return String(status);
  }

  return (
    <SitePage>
      <main className={styles.content}>
        <SectionTitle eyebrow="QUẢN LÝ NỘI DUNG" title="Công thức của tôi" description="Lưu bản nháp, cập nhật hoặc xuất bản nội dung." />
        <div className={styles.toolbar}>
          <h2>{result?.totalCount ?? 0} công thức</h2>
          <Link className={styles.button} href="/dashboard/recipes/new">Tạo công thức</Link>
        </div>
        {loading && <LoadingNotice />}
        {error && <ErrorNotice>{error}</ErrorNotice>}
        {!loading && !error && result?.items.length === 0 && <p className={styles.notice}>Bạn chưa có công thức nào.</p>}
        {!!result?.items.length && <div style={{ overflowX: "auto" }}>
          <table className={styles.table}>
            <thead><tr><th>Tên món</th><th>Trạng thái</th><th>Thời gian nấu</th><th>Thao tác</th></tr></thead>
            <tbody>{result.items.map((recipe) => (
              <tr key={recipe.id}>
                <td><strong>{recipe.title}</strong></td>
                <td><span className={styles.status}>{statusLabel(recipe.status)}</span></td>
                <td>{recipe.cookTimeMinutes} phút</td>
                <td>
                  <div className={styles.actions}>
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
    </SitePage>
  );
}
