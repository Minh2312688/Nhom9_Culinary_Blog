"use client";

import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  createCategory,
  deleteCategory,
  getRecipeCategories,
  updateCategory,
  type RecipeCategory,
} from "@/lib/recipes-api";
import { ErrorNotice, LoadingNotice, SectionTitle, SitePage } from "../../site-ui";
import styles from "../../site-ui.module.css";

export default function DashboardCategoriesPage() {
  const router = useRouter();
  const [token, setToken] = useState("");
  const [categories, setCategories] = useState<RecipeCategory[]>([]);
  const [selected, setSelected] = useState<RecipeCategory | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function loadCategories(signal?: AbortSignal) {
    try {
      setCategories(await getRecipeCategories(signal ?? new AbortController().signal));
      setError(null);
    } catch (reason) {
      if (!signal?.aborted) setError(reason instanceof Error ? reason.message : "Không thể tải danh mục.");
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }

  useEffect(() => {
    const accessToken = sessionStorage.getItem("culinary_access_token");
    if (!accessToken) {
      router.replace("/auth/login");
      return;
    }
    setToken(accessToken);
    const controller = new AbortController();
    void loadCategories(controller.signal);
    return () => controller.abort();
  }, [router]);

  function startEdit(category: RecipeCategory) {
    setSelected(category);
    setName(category.name);
    setDescription(category.description ?? "");
    setError(null);
  }

  function resetForm() {
    setSelected(null);
    setName("");
    setDescription("");
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    try {
      if (selected) {
        await updateCategory(token, selected.id, {
          name: name.trim(),
          description: description.trim() || null,
          imageUrl: selected.imageUrl ?? null,
          orderIndex: selected.orderIndex ?? 0,
        });
      } else {
        await createCategory(token, { name: name.trim(), description: description.trim() || null });
      }
      resetForm();
      await loadCategories();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Không thể lưu danh mục.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(category: RecipeCategory) {
    if (!window.confirm(`Xóa danh mục "${category.name}"?`)) return;
    try {
      await deleteCategory(token, category.id);
      await loadCategories();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Không thể xóa danh mục.");
    }
  }

  return (
    <SitePage>
      <main className={styles.content}>
        <SectionTitle eyebrow="QUẢN TRỊ NỘI DUNG" title="Quản lý danh mục" description="Thêm, cập nhật hoặc xóa danh mục công thức." />
        {error && <ErrorNotice>{error}</ErrorNotice>}
        <form className={styles.formStack} onSubmit={(event) => void onSubmit(event)}>
          <h2>{selected ? "Cập nhật danh mục" : "Thêm danh mục"}</h2>
          <div className={styles.formGrid}>
            <label className={styles.field}>Tên danh mục<input required minLength={2} maxLength={100} value={name} onChange={(event) => setName(event.target.value)} /></label>
            <label className={styles.field}>Mô tả<input maxLength={500} value={description} onChange={(event) => setDescription(event.target.value)} /></label>
          </div>
          <div className={styles.actions}>
            <button className={styles.button} type="submit" disabled={saving}>{saving ? "Đang lưu..." : selected ? "Cập nhật" : "Tạo danh mục"}</button>
            {selected && <button className={styles.buttonSecondary} type="button" onClick={resetForm}>Hủy sửa</button>}
          </div>
        </form>
        <div className={styles.toolbar}><h2>Danh mục hiện có</h2><span>{categories.length} mục</span></div>
        {loading && <LoadingNotice />}
        {!loading && categories.length === 0 && <p className={styles.notice}>Chưa có danh mục.</p>}
        {categories.length > 0 && <div style={{ overflowX: "auto" }}>
          <table className={styles.table}>
            <thead><tr><th>Tên</th><th>Slug</th><th>Số công thức</th><th>Thao tác</th></tr></thead>
            <tbody>{categories.map((category) => (
              <tr key={category.id}>
                <td><strong>{category.name}</strong></td><td>{category.slug}</td><td>{category.recipeCount ?? 0}</td>
                <td><div className={styles.actions}>
                  <button type="button" onClick={() => startEdit(category)}>Sửa</button>
                  <button type="button" onClick={() => void handleDelete(category)}>Xóa</button>
                </div></td>
              </tr>
            ))}</tbody>
          </table>
        </div>}
      </main>
    </SitePage>
  );
}
