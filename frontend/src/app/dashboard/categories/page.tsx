"use client";

import { ChangeEvent, FormEvent, useEffect, useState } from "react";
import { useSession } from "next-auth/react";
import {
  type RecipeCategory,
} from "@/lib/recipes-api";
import { useDashboardCategories } from "@/lib/queries/categories";
import {
  useCreateCategory,
  useDeleteCategory,
  useUpdateCategory,
  useUploadCategoryImage,
} from "@/lib/queries/category-mutations";
import { getFormErrorDetails, useFeedback } from "../../feedback";
import { ErrorNotice, LoadingNotice, SectionTitle } from "../../site-ui";
import styles from "../../site-ui.module.css";

export default function DashboardCategoriesPage() {
  const { showToast } = useFeedback();
  const { data: session, status: sessionStatus } = useSession();
  const userId = session?.user.id ?? "";
  const categoriesQuery = useDashboardCategories(userId, sessionStatus === "authenticated");
  const createCategoryMutation = useCreateCategory(userId);
  const updateCategoryMutation = useUpdateCategory(userId);
  const deleteCategoryMutation = useDeleteCategory(userId);
  const uploadCategoryImageMutation = useUploadCategoryImage(userId);
  const categories = categoriesQuery.data ?? [];
  const loading = sessionStatus === "loading"
    || (sessionStatus === "authenticated" && categoriesQuery.isFetching);
  const [selected, setSelected] = useState<RecipeCategory | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [pendingImageFile, setPendingImageFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [pendingUploadCategoryId, setPendingUploadCategoryId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const uploadingImage = uploadCategoryImageMutation.isPending;
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const queryError = categoriesQuery.error instanceof Error
    ? categoriesQuery.error.message
    : categoriesQuery.error ? "Không thể tải danh mục." : null;
  const displayedError = error ?? queryError;

  useEffect(() => {
    if (!pendingImageFile) {
      setPreviewUrl(null);
      return;
    }

    const url = URL.createObjectURL(pendingImageFile);
    setPreviewUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [pendingImageFile]);

  useEffect(() => {
    if (queryError) showToast(queryError, "error");
  }, [queryError, showToast]);

  function startEdit(category: RecipeCategory) {
    setSelected(category);
    setName(category.name);
    setDescription(category.description ?? "");
    setPendingImageFile(null);
    setPendingUploadCategoryId(null);
    setError(null);
    setFieldErrors({});
  }

  function resetForm() {
    setSelected(null);
    setName("");
    setDescription("");
    setPendingImageFile(null);
    setPendingUploadCategoryId(null);
    setFieldErrors({});
    setError(null);
  }

  function onImageSelected(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0] ?? null;
    if (file && file.size > 5 * 1024 * 1024) {
      event.target.value = "";
      setPendingImageFile(null);
      setError("Ảnh danh mục không được vượt quá 5 MB.");
      return;
    }
    setPendingImageFile(file);
    setError(null);
  }

  async function finishImageUpload(categoryId: string): Promise<boolean> {
    if (!pendingImageFile) return true;

    try {
      await uploadCategoryImageMutation.mutateAsync({ categoryId, file: pendingImageFile });
      setPendingUploadCategoryId(null);
      resetForm();
      showToast(selected ? "Đã cập nhật danh mục." : "Đã tạo danh mục.", "success");
      return true;
    } catch (reason) {
      const uploadMessage = reason instanceof Error ? reason.message : "Không thể tải ảnh danh mục lên.";
      const message = `${selected ? "Đã cập nhật" : "Đã tạo"} danh mục, nhưng ảnh chưa tải lên. ${uploadMessage}`;
      setPendingUploadCategoryId(categoryId);
      setError(message);
      showToast(message, "error");
      return false;
    }
  }

  function clearFieldError(field: string) {
    setFieldErrors((current) => {
      if (!current[field]) return current;
      const next = { ...current };
      delete next[field];
      return next;
    });
    setError(null);
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    setFieldErrors({});
    try {
      if (pendingUploadCategoryId) {
        await finishImageUpload(pendingUploadCategoryId);
        return;
      }

      let savedCategory: RecipeCategory;
      if (selected) {
        savedCategory = await updateCategoryMutation.mutateAsync({
          id: selected.id,
          payload: {
            name: name.trim(),
            description: description.trim() || null,
            imageUrl: selected.imageUrl ?? null,
            orderIndex: selected.orderIndex ?? 0,
          },
        });
      } else {
        savedCategory = await createCategoryMutation.mutateAsync({
          name: name.trim(),
          description: description.trim() || null,
        });
      }

      if (pendingImageFile) {
        setPendingUploadCategoryId(savedCategory.id);
        await finishImageUpload(savedCategory.id);
        return;
      }

      resetForm();
      showToast(selected ? "Đã cập nhật danh mục." : "Đã tạo danh mục.", "success");
    } catch (reason) {
      const details = getFormErrorDetails(reason, ["name", "description"]);
      setFieldErrors(details.fieldErrors);
      setError(details.summary);
      showToast(reason instanceof Error ? reason.message : "Không thể lưu danh mục.", "error");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(category: RecipeCategory) {
    if (!window.confirm(`Xóa danh mục "${category.name}"?`)) return;
    try {
      await deleteCategoryMutation.mutateAsync(category.id);
      showToast("Đã xóa danh mục.", "success");
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Không thể xóa danh mục.";
      setError(message);
      showToast(message, "error");
    }
  }

  return (
      <main className={styles.content}>
        <SectionTitle eyebrow="QUẢN TRỊ NỘI DUNG" title="Quản lý danh mục" description="Thêm, cập nhật hoặc xóa danh mục công thức." />
        {displayedError && <ErrorNotice><span data-error-summary>{displayedError}</span></ErrorNotice>}
        <form className={styles.formStack} onSubmit={(event) => void onSubmit(event)}>
          <h2>{selected ? "Cập nhật danh mục" : "Thêm danh mục"}</h2>
          <div className={styles.formGrid}>
            <label className={styles.field}>Tên danh mục<input id="name" aria-invalid={Boolean(fieldErrors.name)} aria-describedby={fieldErrors.name ? "name-error" : undefined} required minLength={2} maxLength={100} value={name} onChange={(event) => { setName(event.target.value); clearFieldError("name"); }} />{fieldErrors.name && <span className={styles.fieldError} id="name-error" role="alert">{fieldErrors.name.join(" ")}</span>}</label>
            <label className={styles.field}>Mô tả<input id="description" aria-invalid={Boolean(fieldErrors.description)} aria-describedby={fieldErrors.description ? "description-error" : undefined} maxLength={500} value={description} onChange={(event) => { setDescription(event.target.value); clearFieldError("description"); }} />{fieldErrors.description && <span className={styles.fieldError} id="description-error" role="alert">{fieldErrors.description.join(" ")}</span>}</label>
          </div>
          <label className={styles.field} htmlFor="categoryImageFile">Ảnh danh mục
            <input id="categoryImageFile" type="file" accept="image/*" onChange={onImageSelected} disabled={Boolean(pendingUploadCategoryId) || saving} />
          </label>
          {(previewUrl || selected?.imageUrl) && (
            <img
              className={styles.categoryImagePreview}
              src={previewUrl ?? selected?.imageUrl ?? undefined}
              alt={name.trim() || selected?.name || "Ảnh danh mục"}
            />
          )}
          {uploadingImage && <p role="status">Đang tải ảnh danh mục lên...</p>}
          <div className={styles.actions}>
            <button className={`${styles.button} ${styles.prominentActionButton}`} type="submit" disabled={saving || Boolean(pendingUploadCategoryId)}>{saving ? "Đang lưu..." : selected ? "Cập nhật" : "Tạo danh mục"}</button>
            {pendingUploadCategoryId && <button className={styles.button} type="button" onClick={() => void finishImageUpload(pendingUploadCategoryId)} disabled={saving}>Thử tải ảnh lại</button>}
            {selected && <button className={styles.buttonSecondary} type="button" onClick={resetForm} disabled={Boolean(pendingUploadCategoryId)}>Hủy sửa</button>}
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
                <td><div className={`${styles.actions} ${styles.tableActions}`}>
                  <button className={`${styles.buttonSecondary} ${styles.prominentActionButton}`} type="button" onClick={() => startEdit(category)}>Sửa</button>
                  <button className={`${styles.buttonSecondary} ${styles.prominentActionButton}`} type="button" onClick={() => void handleDelete(category)}>Xóa</button>
                </div></td>
              </tr>
            ))}</tbody>
          </table>
        </div>}
      </main>
  );
}
