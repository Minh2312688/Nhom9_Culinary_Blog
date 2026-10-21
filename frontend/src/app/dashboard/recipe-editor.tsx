"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  createRecipe,
  getRecipe,
  getRecipeCategories,
  getRecipes,
  updateRecipe,
  type RecipeCategory,
  type RecipeDetail,
  type RecipeInput,
} from "@/lib/recipes-api";
import { getFormErrorDetails, useFeedback } from "../feedback";
import { ErrorNotice, LoadingSkeleton, SectionTitle } from "../site-ui";
import styles from "../site-ui.module.css";

export function RecipeEditor({ editing = false }: { editing?: boolean }) {
  const router = useRouter();
  const { showToast } = useFeedback();
  const params = useParams<{ id?: string }>();
  const recipeId = params.id;
  const [token, setToken] = useState("");
  const [categories, setCategories] = useState<RecipeCategory[]>([]);
  const [recipe, setRecipe] = useState<RecipeDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [prepTime, setPrepTime] = useState("0");
  const [cookTime, setCookTime] = useState("0");
  const [servings, setServings] = useState("2");
  const [difficulty, setDifficulty] = useState("Easy");
  const [ingredients, setIngredients] = useState("");
  const [steps, setSteps] = useState("");

  useEffect(() => {
    const accessToken = sessionStorage.getItem("culinary_access_token");
    if (!accessToken) {
      router.replace("/auth/login");
      return;
    }
    setToken(accessToken);

    const controller = new AbortController();
    async function load(accessToken: string) {
      try {
        const categoryItems = await getRecipeCategories(controller.signal);
        setCategories(categoryItems);
        if (editing && recipeId) {
          const listed = await getRecipes(controller.signal, 50, accessToken);
          const summary = listed.items.find((item) => item.id === recipeId);
          if (!summary) throw new Error("Không tìm thấy công thức cần chỉnh sửa.");
          const detail = await getRecipe(summary.slug, controller.signal);
          setRecipe(detail);
          setTitle(detail.title);
          setDescription(detail.description ?? "");
          setCategoryId(detail.categoryId);
          setPrepTime(String(detail.prepTimeMinutes));
          setCookTime(String(detail.cookTimeMinutes));
          setServings(String(detail.servings));
          setDifficulty(detail.difficulty);
          setIngredients(detail.ingredients.map((item) =>
            [item.name, item.quantity, item.unit, item.notes].filter(Boolean).join(" | "),
          ).join("\n"));
          setSteps(detail.steps.map((item) => item.description).join("\n"));
        } else if (categoryItems.length) {
          setCategoryId(categoryItems[0].id);
        }
      } catch (reason) {
        if (!controller.signal.aborted) {
          const message = reason instanceof Error ? reason.message : "Không thể tải dữ liệu.";
          setError(message);
          showToast(message, "error");
        }
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    }
    void load(accessToken);
    return () => controller.abort();
  }, [editing, recipeId, router, showToast]);

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
    const payload: RecipeInput = {
      title: title.trim(),
      description: description.trim() || null,
      categoryId,
      prepTimeMinutes: Number(prepTime),
      cookTimeMinutes: Number(cookTime),
      servings: Number(servings),
      difficulty,
      ingredients: ingredients.split("\n").map((line) => line.trim()).filter(Boolean).map((line, orderIndex) => {
        const [name, quantityText, unit, notes] = line.split("|").map((part) => part.trim());
        return {
          name,
          quantity: quantityText && Number.isFinite(Number(quantityText)) ? Number(quantityText) : null,
          unit: unit || null,
          notes: notes || null,
          orderIndex,
        };
      }),
      steps: steps.split("\n").map((description) => description.trim()).filter(Boolean).map((description) => ({
        title: null,
        description,
        durationMinutes: null,
        imageUrl: null,
      })),
    };

    try {
      if (editing && recipe && recipeId) {
        await updateRecipe(token, recipeId, { ...payload, rowVersion: recipe.rowVersion });
      } else {
        await createRecipe(token, payload);
      }
      showToast(editing ? "Đã cập nhật công thức." : "Đã lưu bản nháp.", "success");
      router.push("/dashboard/recipes");
    } catch (reason) {
      const details = getFormErrorDetails(reason, [
        "title",
        "description",
        "categoryId",
        "difficulty",
        "prepTimeMinutes",
        "cookTimeMinutes",
        "servings",
        "ingredients",
        "steps",
      ]);
      setFieldErrors(details.fieldErrors);
      setError(details.summary);
      showToast(reason instanceof Error ? reason.message : "Không thể lưu công thức.", "error");
      setSaving(false);
    }
  }

  function renderFieldError(field: string) {
    const message = fieldErrors[field]?.join(" ");
    return message ? <span className={styles.fieldError} id={`${field}-error`} role="alert">{message}</span> : null;
  }

  return (
      <main className={styles.content}>
        <SectionTitle
          eyebrow="QUẢN LÝ CÔNG THỨC"
          title={editing ? "Chỉnh sửa công thức" : "Tạo công thức mới"}
          description="Thông tin được lưu trực tiếp vào hệ thống công thức."
        />
        {loading && <LoadingSkeleton variant="form" />}
        {error && <ErrorNotice><span data-error-summary>{error}</span></ErrorNotice>}
        {!loading && <form className={styles.formStack} onSubmit={(event) => void onSubmit(event)}>
          <label className={styles.field}>Tên món<input id="title" aria-invalid={Boolean(fieldErrors.title)} aria-describedby={fieldErrors.title ? "title-error" : undefined} required minLength={5} maxLength={200} value={title} onChange={(event) => { setTitle(event.target.value); clearFieldError("title"); }} />{renderFieldError("title")}</label>
          <label className={styles.field}>Mô tả<textarea id="description" aria-invalid={Boolean(fieldErrors.description)} aria-describedby={fieldErrors.description ? "description-error" : undefined} value={description} onChange={(event) => { setDescription(event.target.value); clearFieldError("description"); }} />{renderFieldError("description")}</label>
          <div className={styles.formGrid}>
            <label className={styles.field}>Danh mục<select id="categoryId" aria-invalid={Boolean(fieldErrors.categoryId)} aria-describedby={fieldErrors.categoryId ? "categoryId-error" : undefined} required value={categoryId} onChange={(event) => { setCategoryId(event.target.value); clearFieldError("categoryId"); }}>
              <option value="">Chọn danh mục</option>{categories.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>{renderFieldError("categoryId")}</label>
            <label className={styles.field}>Độ khó<select id="difficulty" aria-invalid={Boolean(fieldErrors.difficulty)} aria-describedby={fieldErrors.difficulty ? "difficulty-error" : undefined} value={difficulty} onChange={(event) => { setDifficulty(event.target.value); clearFieldError("difficulty"); }}>
              <option value="Easy">Dễ</option><option value="Medium">Trung bình</option><option value="Hard">Khó</option><option value="Expert">Chuyên gia</option>
            </select>{renderFieldError("difficulty")}</label>
            <label className={styles.field}>Thời gian chuẩn bị (phút)<input id="prepTimeMinutes" aria-invalid={Boolean(fieldErrors.prepTimeMinutes)} aria-describedby={fieldErrors.prepTimeMinutes ? "prepTimeMinutes-error" : undefined} type="number" min="0" required value={prepTime} onChange={(event) => { setPrepTime(event.target.value); clearFieldError("prepTimeMinutes"); }} />{renderFieldError("prepTimeMinutes")}</label>
            <label className={styles.field}>Thời gian nấu (phút)<input id="cookTimeMinutes" aria-invalid={Boolean(fieldErrors.cookTimeMinutes)} aria-describedby={fieldErrors.cookTimeMinutes ? "cookTimeMinutes-error" : undefined} type="number" min="0" required value={cookTime} onChange={(event) => { setCookTime(event.target.value); clearFieldError("cookTimeMinutes"); }} />{renderFieldError("cookTimeMinutes")}</label>
            <label className={styles.field}>Khẩu phần<input id="servings" aria-invalid={Boolean(fieldErrors.servings)} aria-describedby={fieldErrors.servings ? "servings-error" : undefined} type="number" min="1" required value={servings} onChange={(event) => { setServings(event.target.value); clearFieldError("servings"); }} />{renderFieldError("servings")}</label>
          </div>
          <label className={styles.field}>Nguyên liệu (mỗi dòng: tên | số lượng | đơn vị | ghi chú)<textarea id="ingredients" aria-invalid={Boolean(fieldErrors.ingredients)} aria-describedby={fieldErrors.ingredients ? "ingredients-error" : undefined} value={ingredients} onChange={(event) => { setIngredients(event.target.value); clearFieldError("ingredients"); }} placeholder={"Thịt bò | 300 | g\nHành lá"} />{renderFieldError("ingredients")}</label>
          <label className={styles.field}>Các bước chế biến (mỗi bước một dòng)<textarea id="steps" aria-invalid={Boolean(fieldErrors.steps)} aria-describedby={fieldErrors.steps ? "steps-error" : undefined} value={steps} onChange={(event) => { setSteps(event.target.value); clearFieldError("steps"); }} placeholder={"Sơ chế nguyên liệu.\nNấu và nêm nếm."} />{renderFieldError("steps")}</label>
          <div className={styles.actions}>
            <button className={styles.button} type="submit" disabled={saving || !categoryId}>{saving ? "Đang lưu..." : "Lưu bản nháp"}</button>
            <Link className={styles.buttonSecondary} href="/dashboard/recipes">Hủy</Link>
          </div>
        </form>}
      </main>
  );
}
