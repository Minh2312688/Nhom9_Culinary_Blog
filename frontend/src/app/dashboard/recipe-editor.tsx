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
import { ErrorNotice, LoadingNotice, SectionTitle, SitePage } from "../site-ui";
import styles from "../site-ui.module.css";

export function RecipeEditor({ editing = false }: { editing?: boolean }) {
  const router = useRouter();
  const params = useParams<{ id?: string }>();
  const recipeId = params.id;
  const [token, setToken] = useState("");
  const [categories, setCategories] = useState<RecipeCategory[]>([]);
  const [recipe, setRecipe] = useState<RecipeDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
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
        if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Không thể tải dữ liệu.");
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    }
    void load(accessToken);
    return () => controller.abort();
  }, [editing, recipeId, router]);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setError(null);
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
      router.push("/dashboard/recipes");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Không thể lưu công thức.");
      setSaving(false);
    }
  }

  return (
    <SitePage>
      <main className={styles.content}>
        <SectionTitle
          eyebrow="QUẢN LÝ CÔNG THỨC"
          title={editing ? "Chỉnh sửa công thức" : "Tạo công thức mới"}
          description="Thông tin được lưu trực tiếp vào hệ thống công thức."
        />
        {loading && <LoadingNotice />}
        {error && <ErrorNotice>{error}</ErrorNotice>}
        {!loading && <form className={styles.formStack} onSubmit={(event) => void onSubmit(event)}>
          <label className={styles.field}>Tên món<input required minLength={5} maxLength={200} value={title} onChange={(event) => setTitle(event.target.value)} /></label>
          <label className={styles.field}>Mô tả<textarea value={description} onChange={(event) => setDescription(event.target.value)} /></label>
          <div className={styles.formGrid}>
            <label className={styles.field}>Danh mục<select required value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
              <option value="">Chọn danh mục</option>{categories.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select></label>
            <label className={styles.field}>Độ khó<select value={difficulty} onChange={(event) => setDifficulty(event.target.value)}>
              <option value="Easy">Dễ</option><option value="Medium">Trung bình</option><option value="Hard">Khó</option><option value="Expert">Chuyên gia</option>
            </select></label>
            <label className={styles.field}>Thời gian chuẩn bị (phút)<input type="number" min="0" required value={prepTime} onChange={(event) => setPrepTime(event.target.value)} /></label>
            <label className={styles.field}>Thời gian nấu (phút)<input type="number" min="0" required value={cookTime} onChange={(event) => setCookTime(event.target.value)} /></label>
            <label className={styles.field}>Khẩu phần<input type="number" min="1" required value={servings} onChange={(event) => setServings(event.target.value)} /></label>
          </div>
          <label className={styles.field}>Nguyên liệu (mỗi dòng: tên | số lượng | đơn vị | ghi chú)<textarea value={ingredients} onChange={(event) => setIngredients(event.target.value)} placeholder={"Thịt bò | 300 | g\nHành lá"} /></label>
          <label className={styles.field}>Các bước chế biến (mỗi bước một dòng)<textarea value={steps} onChange={(event) => setSteps(event.target.value)} placeholder={"Sơ chế nguyên liệu.\nNấu và nêm nếm."} /></label>
          <div className={styles.actions}>
            <button className={styles.button} type="submit" disabled={saving || !categoryId}>{saving ? "Đang lưu..." : "Lưu bản nháp"}</button>
            <Link className={styles.buttonSecondary} href="/dashboard/recipes">Hủy</Link>
          </div>
        </form>}
      </main>
    </SitePage>
  );
}
