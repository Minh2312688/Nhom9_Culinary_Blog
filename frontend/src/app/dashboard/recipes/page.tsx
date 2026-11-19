"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useSession } from "next-auth/react";
import { useDashboardRecipes } from "@/lib/queries/recipes";
import {
  useArchiveRecipe,
  useDeleteRecipe,
  usePublishRecipe,
} from "@/lib/queries/recipe-mutations";
import { useFeedback } from "../../feedback";
import { ErrorNotice, LoadingSkeleton, Pagination, SectionTitle } from "../../site-ui";
import styles from "../../site-ui.module.css";

export default function DashboardRecipesPage() {
  const { showToast } = useFeedback();
  const { data: session, status: sessionStatus } = useSession();
  const isAdmin = session?.user.roles?.includes("Admin") ?? false;
  const userId = session?.user.id ?? "";
  const [page, setPage] = useState(1);
  const [initialized, setInitialized] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const recipesQuery = useDashboardRecipes(
    userId,
    page,
    isAdmin ? "all" : "mine",
    initialized && sessionStatus === "authenticated",
  );
  const recipeAccessScope = isAdmin ? "admin" : "owner";
  const deleteRecipeMutation = useDeleteRecipe(userId, recipeAccessScope);
  const publishRecipeMutation = usePublishRecipe(userId, recipeAccessScope);
  const archiveRecipeMutation = useArchiveRecipe(userId, recipeAccessScope);
  const result = recipesQuery.data ?? null;
  const loading = !initialized || sessionStatus === "loading" || recipesQuery.isFetching;
  const error = actionError ?? (
    recipesQuery.error instanceof Error
      ? recipesQuery.error.message
      : recipesQuery.error ? "Không thể tải công thức." : null
  );

  useEffect(() => {
    const requestedPage = Number(new URLSearchParams(window.location.search).get("page"));
    setPage(Number.isSafeInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1);
    setInitialized(true);
  }, []);

  useEffect(() => {
    if (!initialized || sessionStatus === "loading") return;
    window.history.replaceState(null, "", page === 1 ? "/dashboard/recipes" : `/dashboard/recipes?page=${page}`);
    setActionError(null);
  }, [initialized, page, sessionStatus]);

  useEffect(() => {
    if (!recipesQuery.error) return;
    const message = recipesQuery.error instanceof Error
      ? recipesQuery.error.message
      : "Không thể tải công thức.";
    showToast(message, "error");
  }, [recipesQuery.error, showToast]);

  async function handlePublish(id: string) {
    try {
      const recipe = result?.items.find((item) => item.id === id);
      await publishRecipeMutation.mutateAsync({ id, slug: recipe?.slug });
      setActionError(null);
      showToast("Đã xuất bản công thức.", "success");
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Không thể xuất bản công thức.";
      setActionError(message);
      showToast(message, "error");
    }
  }

  async function handleArchive(id: string) {
    try {
      const recipe = result?.items.find((item) => item.id === id);
      await archiveRecipeMutation.mutateAsync({ id, slug: recipe?.slug });
      setActionError(null);
      showToast("Đã lưu trữ công thức.", "success");
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Không thể lưu trữ công thức.";
      setActionError(message);
      showToast(message, "error");
    }
  }

  async function handleDelete(id: string) {
    if (!window.confirm("Bạn có chắc muốn xóa công thức này?")) return;
    try {
      const recipe = result?.items.find((item) => item.id === id);
      await deleteRecipeMutation.mutateAsync({ id, slug: recipe?.slug });
      setActionError(null);
      showToast("Đã xóa công thức.", "success");
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Không thể xóa công thức.";
      setActionError(message);
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
        <SectionTitle
          eyebrow="QUẢN LÝ NỘI DUNG"
          title={isAdmin ? "Quản lý công thức" : "Công thức của tôi"}
          description={isAdmin
            ? "Tạo, cập nhật và quản lý công thức của tất cả tác giả."
            : "Lưu bản nháp, cập nhật hoặc xuất bản nội dung."}
        />
        <div className={styles.toolbar}>
          <h2>{result?.totalCount ?? 0} công thức</h2>
          <Link className={styles.button} href="/dashboard/recipes/new">Tạo công thức</Link>
        </div>
        {loading && <LoadingSkeleton variant="cards" />}
        {error && <ErrorNotice><span data-error-summary>{error}</span></ErrorNotice>}
        {!loading && !error && result?.items.length === 0 && (
          <section className={`${styles.notice} ${styles.emptyState}`} aria-label="Chưa có công thức">
            <p>{isAdmin ? "Chưa có công thức nào." : "Bạn chưa có công thức nào."}</p>
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
                    <Link href={recipe.status === 1 || recipe.status === "Published"
                      ? `/recipes/${encodeURIComponent(recipe.slug)}`
                      : `/dashboard/recipes/${encodeURIComponent(recipe.id)}`}>Xem</Link>
                    <Link href={`/dashboard/recipes/${recipe.id}/edit`}>Sửa</Link>
                    {(recipe.status === 0 || recipe.status === "Draft") &&
                      <button type="button" onClick={() => void handlePublish(recipe.id)}>Xuất bản</button>}
                    {isAdmin && (recipe.status === 1 || recipe.status === "Published") &&
                      <button type="button" onClick={() => void handleArchive(recipe.id)}>Lưu trữ</button>}
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
