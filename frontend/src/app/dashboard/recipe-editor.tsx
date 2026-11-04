"use client";

import Link from "next/link";
import { FormEvent, useEffect, useRef, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  createRecipe,
  getRecipe,
  getRecipeCategories,
  getRecipes,
  updateRecipe,
  uploadRecipeImage,
  setPrimaryRecipeImage,
  deleteRecipeImage,
  type RecipeCategory,
  type RecipeDetail,
  type RecipeInput,
  type RecipeNutritionInput,
  type RecipeStep,
  ApiRequestError,
} from "@/lib/recipes-api";
import { getFormErrorDetails, useFeedback } from "../feedback";
import { ErrorNotice, LoadingSkeleton, SectionTitle } from "../site-ui";
import styles from "../site-ui.module.css";

interface StructuredIngredient {
  name: string;
  quantity: string;
  unit: string;
  notes: string;
}

export interface StructuredStep {
  id?: string;
  stepNumber?: number;
  title: string;
  description: string;
  durationMinutes: number | null;
  imageUrl: string | null;
}

function parseIngredientsText(text: string): StructuredIngredient[] {
  return text
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const [name, quantity, unit, notes] = line.split("|").map((p) => p.trim());
      return {
        name: name || "",
        quantity: quantity || "",
        unit: unit || "",
        notes: notes || "",
      };
    });
}

function serializeIngredients(items: StructuredIngredient[]): string {
  return items
    .map((item) =>
      [item.name, item.quantity, item.unit, item.notes].filter(Boolean).join(" | "),
    )
    .join("\n");
}

function parseStepsText(text: string): StructuredStep[] {
  return text
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .map((description, idx) => ({
      stepNumber: idx + 1,
      title: "",
      description,
      durationMinutes: null,
      imageUrl: null,
    }));
}

interface ImageEntry {
  id: string;
  originalUrl: string;
  thumbnailUrl: string | null;
  altText: string | null;
  isPrimary: boolean;
  orderIndex: number;
}

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
  const [isConflict, setIsConflict] = useState(false);

  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [prepTime, setPrepTime] = useState("0");
  const [cookTime, setCookTime] = useState("0");
  const [servings, setServings] = useState("2");
  const [difficulty, setDifficulty] = useState("Easy");
  const [ingredients, setIngredients] = useState("");
  const [steps, setSteps] = useState("");
  const [structuredSteps, setStructuredSteps] = useState<StructuredStep[]>([]);

  // Nutrition state
  const [includeNutrition, setIncludeNutrition] = useState(false);
  const [nutrition, setNutrition] = useState<RecipeNutritionInput>({
    calories: 0,
    protein: 0,
    carbohydrates: 0,
    fat: 0,
    fiber: 0,
    sodium: 0,
  });

  // Image state
  const [images, setImages] = useState<ImageEntry[]>([]);
  const [imageAltText, setImageAltText] = useState("");
  const [imageSetPrimary, setImageSetPrimary] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const structuredIngredients = parseIngredientsText(ingredients);

  async function loadRecipeData(accessToken: string, signal?: AbortSignal) {
    const categoryItems = await getRecipeCategories(signal ?? new AbortController().signal);
    setCategories(categoryItems);
    if (editing && recipeId) {
      const listed = await getRecipes(signal ?? new AbortController().signal, 50, accessToken);
      const summary = listed.items.find((item) => item.id === recipeId);
      if (!summary) throw new Error("Không tìm thấy công thức cần chỉnh sửa.");
      const detail = await getRecipe(summary.slug, signal);
      setRecipe(detail);
      setTitle(detail.title);
      setDescription(detail.description ?? "");
      setCategoryId(detail.categoryId);
      setPrepTime(String(detail.prepTimeMinutes));
      setCookTime(String(detail.cookTimeMinutes));
      setServings(String(detail.servings));
      setDifficulty(detail.difficulty);
      setIngredients(
        detail.ingredients
          .map((item) =>
            [item.name, item.quantity, item.unit, item.notes].filter(Boolean).join(" | "),
          )
          .join("\n"),
      );

      if (detail.steps && detail.steps.length > 0) {
        const sorted = detail.steps
          .slice()
          .sort((a, b) => (a.stepNumber ?? 0) - (b.stepNumber ?? 0));
        setStructuredSteps(
          sorted.map((st, idx) => ({
            id: st.id,
            stepNumber: st.stepNumber ?? idx + 1,
            title: st.title ?? "",
            description: st.description ?? "",
            durationMinutes: st.durationMinutes ?? null,
            imageUrl: st.imageUrl ?? null,
          })),
        );
        setSteps(sorted.map((item) => item.description).join("\n"));
      } else {
        setStructuredSteps([]);
        setSteps("");
      }

      if (detail.nutrition) {
        setIncludeNutrition(true);
        setNutrition({
          calories: detail.nutrition.calories,
          protein: detail.nutrition.protein,
          carbohydrates: detail.nutrition.carbohydrates,
          fat: detail.nutrition.fat,
          fiber: detail.nutrition.fiber,
          sodium: detail.nutrition.sodium,
        });
      }
      setImages(
        detail.images.map((img) => ({
          id: img.id,
          originalUrl: img.originalUrl,
          thumbnailUrl: img.thumbnailUrl,
          altText: img.altText,
          isPrimary: img.isPrimary,
          orderIndex: img.orderIndex,
        })),
      );
    } else if (categoryItems.length) {
      setCategoryId(categoryItems[0].id);
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
    async function load(tok: string) {
      try {
        await loadRecipeData(tok, controller.signal);
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

  async function reloadRecipe() {
    setLoading(true);
    setIsConflict(false);
    setError(null);
    setFieldErrors({});
    try {
      await loadRecipeData(token);
      showToast("Đã tải lại dữ liệu mới nhất.", "success");
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Không thể tải lại công thức.";
      setError(message);
      showToast(message, "error");
    } finally {
      setLoading(false);
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

  function updateIngredientRow(index: number, updated: Partial<StructuredIngredient>) {
    const list = [...structuredIngredients];
    if (!list[index]) return;
    list[index] = { ...list[index], ...updated };
    setIngredients(serializeIngredients(list));
    clearFieldError("ingredients");
  }

  function addIngredientRow() {
    const list = [...structuredIngredients, { name: "", quantity: "", unit: "", notes: "" }];
    setIngredients(serializeIngredients(list) + (ingredients ? "\nMới" : "Mới"));
    clearFieldError("ingredients");
  }

  function removeIngredientRow(index: number) {
    const list = structuredIngredients.filter((_, i) => i !== index);
    setIngredients(serializeIngredients(list));
    clearFieldError("ingredients");
  }

  // Structured Steps Handlers
  function addStepRow() {
    setStructuredSteps((prev) => [
      ...prev,
      {
        stepNumber: prev.length + 1,
        title: "",
        description: "",
        durationMinutes: null,
        imageUrl: null,
      },
    ]);
    clearFieldError("steps");
  }

  function removeStepRow(index: number) {
    setStructuredSteps((prev) => {
      const next = prev.filter((_, i) => i !== index);
      return next.map((st, i) => ({ ...st, stepNumber: i + 1 }));
    });
    clearFieldError("steps");
  }

  function updateStepField<K extends keyof StructuredStep>(
    index: number,
    field: K,
    value: StructuredStep[K],
  ) {
    setStructuredSteps((prev) => {
      const next = [...prev];
      if (!next[index]) return prev;
      next[index] = { ...next[index], [field]: value };
      return next;
    });
    clearFieldError("steps");
  }

  function moveStep(index: number, direction: -1 | 1) {
    setStructuredSteps((prev) => {
      const targetIndex = index + direction;
      if (targetIndex < 0 || targetIndex >= prev.length) return prev;
      const next = [...prev];
      const temp = next[index];
      next[index] = next[targetIndex];
      next[targetIndex] = temp;
      return next.map((st, i) => ({ ...st, stepNumber: i + 1 }));
    });
    clearFieldError("steps");
  }

  function syncStepsFromText() {
    if (!steps.trim()) return;
    const parsed = parseStepsText(steps);
    setStructuredSteps(parsed);
    clearFieldError("steps");
    showToast("Đã đồng bộ các bước từ văn bản.", "success");
  }

  async function handleImageUpload(file: File) {
    if (!recipeId) {
      showToast("Hãy lưu bản nháp trước để có thể tải ảnh lên.", "error");
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      showToast("File quá lớn (tối đa 5MB).", "error");
      return;
    }
    if (images.length >= 10) {
      showToast("Đã đạt giới hạn 10 ảnh.", "error");
      return;
    }
    setUploadingImage(true);
    try {
      const result = await uploadRecipeImage(
        token,
        recipeId,
        file,
        imageSetPrimary,
        imageAltText || undefined,
      );
      const newImg: ImageEntry = {
        id: result.imageId,
        originalUrl: result.originalUrl,
        thumbnailUrl: result.thumbnailUrl,
        altText: result.altText,
        isPrimary: result.isPrimary,
        orderIndex: images.length,
      };
      setImages((prev) =>
        imageSetPrimary
          ? prev.map((img) => ({ ...img, isPrimary: false })).concat(newImg)
          : [...prev, newImg],
      );
      setImageAltText("");
      setImageSetPrimary(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
      showToast("Ảnh đã được tải lên.", "success");
    } catch (reason) {
      showToast(reason instanceof Error ? reason.message : "Không thể tải ảnh.", "error");
    } finally {
      setUploadingImage(false);
    }
  }

  async function handleSetPrimary(imageId: string) {
    if (!recipeId) return;
    try {
      await setPrimaryRecipeImage(token, recipeId, imageId);
      setImages((prev) => prev.map((img) => ({ ...img, isPrimary: img.id === imageId })));
      showToast("Đã đặt ảnh chính.", "success");
    } catch (reason) {
      showToast(reason instanceof Error ? reason.message : "Không thể đặt ảnh chính.", "error");
    }
  }

  async function handleDeleteImage(imageId: string) {
    if (!recipeId || !confirm("Xóa ảnh này?")) return;
    try {
      await deleteRecipeImage(token, recipeId, imageId);
      setImages((prev) => prev.filter((img) => img.id !== imageId));
      showToast("Đã xóa ảnh.", "success");
    } catch (reason) {
      showToast(reason instanceof Error ? reason.message : "Không thể xóa ảnh.", "error");
    }
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    setFieldErrors({});
    setIsConflict(false);

    // Resolve steps: prefer structuredSteps if present, otherwise parse from textarea
    const resolvedSteps: RecipeStep[] =
      structuredSteps.length > 0
        ? structuredSteps
            .filter((st) => st.description.trim() || st.title.trim())
            .map((st, idx) => ({
              id: st.id,
              stepNumber: idx + 1,
              title: st.title.trim() || null,
              description: st.description.trim(),
              durationMinutes:
                st.durationMinutes && Number(st.durationMinutes) > 0
                  ? Number(st.durationMinutes)
                  : null,
              imageUrl: st.imageUrl?.trim() || null,
            }))
        : steps
            .split("\n")
            .map((desc) => desc.trim())
            .filter(Boolean)
            .map((desc, idx) => ({
              stepNumber: idx + 1,
              title: null,
              description: desc,
              durationMinutes: null,
              imageUrl: null,
            }));

    const payload: RecipeInput = {
      title: title.trim(),
      description: description.trim() || null,
      categoryId,
      prepTimeMinutes: Number(prepTime),
      cookTimeMinutes: Number(cookTime),
      servings: Number(servings),
      difficulty,
      ingredients: ingredients
        .split("\n")
        .map((line) => line.trim())
        .filter(Boolean)
        .map((line, orderIndex) => {
          const [name, quantityText, unit, notes] = line.split("|").map((part) => part.trim());
          return {
            name,
            quantity:
              quantityText && Number.isFinite(Number(quantityText)) ? Number(quantityText) : null,
            unit: unit || null,
            notes: notes || null,
            orderIndex,
          };
        }),
      steps: resolvedSteps,
      nutrition: includeNutrition ? nutrition : null,
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
      if (
        (reason instanceof ApiRequestError && reason.status === 409) ||
        (reason instanceof Error && reason.message.includes("409"))
      ) {
        setIsConflict(true);
        setError(
          "Công thức này đã được cập nhật bởi phiên làm việc khác. Vui lòng tải lại dữ liệu để nhận phiên bản mới nhất.",
        );
        showToast("Xung đột phiên bản: Công thức đã bị sửa ở phiên khác.", "error");
        setSaving(false);
        return;
      }

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
    return message ? (
      <span className={styles.fieldError} id={`${field}-error`} role="alert">
        {message}
      </span>
    ) : null;
  }

  return (
    <main className={styles.content}>
      <SectionTitle
        eyebrow="QUẢN LÝ CÔNG THỨC"
        title={editing ? "Chỉnh sửa công thức" : "Tạo công thức mới"}
        description="Thông tin được lưu trực tiếp vào hệ thống công thức."
      />
      {loading && <LoadingSkeleton variant="form" />}
      {isConflict && (
        <div
          role="alert"
          style={{
            padding: "16px",
            border: "1px solid #f87171",
            borderRadius: "6px",
            backgroundColor: "#fef2f2",
            color: "#991b1b",
            marginBottom: "16px",
            display: "grid",
            gap: "10px",
          }}
        >
          <strong style={{ fontSize: "14px" }}>
            ⚠️ Xung đột phiên bản dữ liệu (Optimistic Concurrency)
          </strong>
          <span data-error-summary>
            Công thức này đã được cập nhật bởi phiên làm việc khác. Vui lòng tải lại dữ liệu để nhận
            phiên bản mới nhất.
          </span>
          <div>
            <button
              type="button"
              className={styles.buttonSecondary}
              style={{ fontSize: "12px", minHeight: "36px", padding: "0 12px" }}
              onClick={() => void reloadRecipe()}
            >
              🔄 Tải lại dữ liệu
            </button>
          </div>
        </div>
      )}
      {!loading && (
        <form className={styles.form} onSubmit={(e) => void onSubmit(e)} noValidate>
          {error && !isConflict && <ErrorNotice>{error}</ErrorNotice>}

          <label className={styles.field}>
            Tiêu đề
            <input
              id="title"
              aria-invalid={Boolean(fieldErrors.title)}
              aria-describedby={fieldErrors.title ? "title-error" : undefined}
              required
              minLength={5}
              maxLength={200}
              value={title}
              onChange={(event) => {
                setTitle(event.target.value);
                clearFieldError("title");
              }}
            />
            {renderFieldError("title")}
          </label>
          <label className={styles.field}>
            Mô tả
            <textarea
              id="description"
              aria-invalid={Boolean(fieldErrors.description)}
              aria-describedby={fieldErrors.description ? "description-error" : undefined}
              value={description}
              onChange={(event) => {
                setDescription(event.target.value);
                clearFieldError("description");
              }}
            />
            {renderFieldError("description")}
          </label>
          <div className={styles.formGrid}>
            <label className={styles.field}>
              Danh mục
              <select
                id="categoryId"
                aria-invalid={Boolean(fieldErrors.categoryId)}
                aria-describedby={fieldErrors.categoryId ? "categoryId-error" : undefined}
                required
                value={categoryId}
                onChange={(event) => {
                  setCategoryId(event.target.value);
                  clearFieldError("categoryId");
                }}
              >
                <option value="">Chọn danh mục</option>
                {categories.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
              </select>
              {renderFieldError("categoryId")}
            </label>
            <label className={styles.field}>
              Độ khó
              <select
                id="difficulty"
                aria-invalid={Boolean(fieldErrors.difficulty)}
                aria-describedby={fieldErrors.difficulty ? "difficulty-error" : undefined}
                value={difficulty}
                onChange={(event) => {
                  setDifficulty(event.target.value);
                  clearFieldError("difficulty");
                }}
              >
                <option value="Easy">Dễ</option>
                <option value="Medium">Trung bình</option>
                <option value="Hard">Khó</option>
                <option value="Expert">Chuyên gia</option>
              </select>
              {renderFieldError("difficulty")}
            </label>
            <label className={styles.field}>
              Thời gian chuẩn bị (phút)
              <input
                id="prepTimeMinutes"
                aria-invalid={Boolean(fieldErrors.prepTimeMinutes)}
                aria-describedby={fieldErrors.prepTimeMinutes ? "prepTimeMinutes-error" : undefined}
                type="number"
                min="0"
                required
                value={prepTime}
                onChange={(event) => {
                  setPrepTime(event.target.value);
                  clearFieldError("prepTimeMinutes");
                }}
              />
              {renderFieldError("prepTimeMinutes")}
            </label>
            <label className={styles.field}>
              Thời gian nấu (phút)
              <input
                id="cookTimeMinutes"
                aria-invalid={Boolean(fieldErrors.cookTimeMinutes)}
                aria-describedby={fieldErrors.cookTimeMinutes ? "cookTimeMinutes-error" : undefined}
                type="number"
                min="0"
                required
                value={cookTime}
                onChange={(event) => {
                  setCookTime(event.target.value);
                  clearFieldError("cookTimeMinutes");
                }}
              />
              {renderFieldError("cookTimeMinutes")}
            </label>
            <label className={styles.field}>
              Khẩu phần
              <input
                id="servings"
                aria-invalid={Boolean(fieldErrors.servings)}
                aria-describedby={fieldErrors.servings ? "servings-error" : undefined}
                type="number"
                min="1"
                required
                value={servings}
                onChange={(event) => {
                  setServings(event.target.value);
                  clearFieldError("servings");
                }}
              />
              {renderFieldError("servings")}
            </label>
          </div>

          <label className={styles.field}>
            Nguyên liệu (mỗi dòng: tên | số lượng | đơn vị | ghi chú)
            <textarea
              id="ingredients"
              aria-invalid={Boolean(fieldErrors.ingredients)}
              aria-describedby={fieldErrors.ingredients ? "ingredients-error" : undefined}
              value={ingredients}
              onChange={(event) => {
                setIngredients(event.target.value);
                clearFieldError("ingredients");
              }}
              placeholder={"Thịt bò | 300 | g\nHành lá"}
            />
            {renderFieldError("ingredients")}
          </label>

          {structuredIngredients.length > 0 && (
            <div
              style={{
                display: "grid",
                gap: "8px",
                background: "var(--color-surface)",
                padding: "12px",
                borderRadius: "6px",
                border: "1px solid var(--line)",
              }}
            >
              <div
                style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}
              >
                <span
                  style={{ fontWeight: 600, fontSize: "12px", color: "var(--forest)" }}
                >
                  Bảng chi tiết nguyên liệu ({structuredIngredients.length})
                </span>
                <button
                  type="button"
                  className={styles.buttonSecondary}
                  style={{ minHeight: "30px", fontSize: "11px", padding: "0 8px" }}
                  onClick={addIngredientRow}
                >
                  + Thêm dòng
                </button>
              </div>
              {structuredIngredients.map((ing, idx) => (
                <div
                  key={idx}
                  style={{
                    display: "grid",
                    gridTemplateColumns: "2fr 1fr 1fr 2fr auto",
                    gap: "8px",
                    alignItems: "center",
                  }}
                >
                  <input
                    aria-label={`Tên nguyên liệu ${idx + 1}`}
                    placeholder="Tên nguyên liệu"
                    value={ing.name}
                    onChange={(e) => updateIngredientRow(idx, { name: e.target.value })}
                    style={{ minHeight: "34px", padding: "4px 8px", fontSize: "12px" }}
                  />
                  <input
                    aria-label={`Định lượng ${idx + 1}`}
                    placeholder="Số lượng"
                    value={ing.quantity}
                    onChange={(e) => updateIngredientRow(idx, { quantity: e.target.value })}
                    style={{ minHeight: "34px", padding: "4px 8px", fontSize: "12px" }}
                  />
                  <input
                    aria-label={`Đơn vị ${idx + 1}`}
                    placeholder="Đơn vị"
                    value={ing.unit}
                    onChange={(e) => updateIngredientRow(idx, { unit: e.target.value })}
                    style={{ minHeight: "34px", padding: "4px 8px", fontSize: "12px" }}
                  />
                  <input
                    aria-label={`Ghi chú ${idx + 1}`}
                    placeholder="Ghi chú"
                    value={ing.notes}
                    onChange={(e) => updateIngredientRow(idx, { notes: e.target.value })}
                    style={{ minHeight: "34px", padding: "4px 8px", fontSize: "12px" }}
                  />
                  <button
                    type="button"
                    title="Xóa dòng"
                    aria-label={`Xóa nguyên liệu ${idx + 1}`}
                    style={{
                      color: "var(--color-error)",
                      background: "none",
                      border: "none",
                      cursor: "pointer",
                      fontSize: "16px",
                      padding: "4px",
                    }}
                    onClick={() => removeIngredientRow(idx)}
                  >
                    ×
                  </button>
                </div>
              ))}
            </div>
          )}

          {/* Structured Steps Section */}
          <div
            style={{
              display: "grid",
              gap: "12px",
              background: "var(--color-surface)",
              padding: "16px",
              borderRadius: "6px",
              border: "1px solid var(--line)",
            }}
          >
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                flexWrap: "wrap",
                gap: "8px",
              }}
            >
              <div>
                <span
                  style={{
                    fontWeight: 700,
                    fontSize: "13px",
                    color: "var(--forest)",
                    display: "block",
                  }}
                >
                  Các bước chế biến ({structuredSteps.length} bước)
                </span>
                <small style={{ color: "var(--color-muted)", fontSize: "11px" }}>
                  Mỗi bước hỗ trợ tiêu đề, thời gian, mô tả thao tác và link ảnh minh họa. Dùng nút
                  mũi tên để sắp xếp thứ tự.
                </small>
              </div>
              <div style={{ display: "flex", gap: "6px" }}>
                {steps && structuredSteps.length === 0 && (
                  <button
                    type="button"
                    className={styles.buttonSecondary}
                    style={{ minHeight: "30px", fontSize: "11px", padding: "0 8px" }}
                    onClick={syncStepsFromText}
                  >
                    ⚡ Tạo từ văn bản
                  </button>
                )}
                <button
                  id="add-step-button"
                  type="button"
                  className={styles.buttonSecondary}
                  style={{ minHeight: "30px", fontSize: "11px", padding: "0 10px" }}
                  onClick={addStepRow}
                >
                  + Thêm bước nấu
                </button>
              </div>
            </div>

            {renderFieldError("steps")}

            {/* List of structured steps */}
            {structuredSteps.map((st, idx) => (
              <div
                key={idx}
                data-testid={`structured-step-${idx}`}
                style={{
                  display: "grid",
                  gap: "8px",
                  padding: "12px",
                  borderRadius: "6px",
                  border: "1px solid var(--line)",
                  background: "#fff",
                }}
              >
                <div
                  style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                    <span
                      style={{
                        fontSize: "12px",
                        fontWeight: 700,
                        padding: "2px 8px",
                        borderRadius: "12px",
                        background: "var(--color-surface)",
                        border: "1px solid var(--line)",
                        color: "var(--forest)",
                      }}
                    >
                      Bước {idx + 1}
                    </span>
                  </div>
                  <div style={{ display: "flex", gap: "4px", alignItems: "center" }}>
                    <button
                      type="button"
                      data-testid={`step-move-up-${idx}`}
                      title="Di chuyển lên"
                      aria-label={`Di chuyển bước ${idx + 1} lên`}
                      disabled={idx === 0}
                      onClick={() => moveStep(idx, -1)}
                      style={{
                        padding: "2px 8px",
                        fontSize: "12px",
                        cursor: idx === 0 ? "not-allowed" : "pointer",
                        opacity: idx === 0 ? 0.3 : 1,
                        border: "1px solid var(--line)",
                        borderRadius: "4px",
                        background: "none",
                      }}
                    >
                      ↑
                    </button>
                    <button
                      type="button"
                      data-testid={`step-move-down-${idx}`}
                      title="Di chuyển xuống"
                      aria-label={`Di chuyển bước ${idx + 1} xuống`}
                      disabled={idx === structuredSteps.length - 1}
                      onClick={() => moveStep(idx, 1)}
                      style={{
                        padding: "2px 8px",
                        fontSize: "12px",
                        cursor: idx === structuredSteps.length - 1 ? "not-allowed" : "pointer",
                        opacity: idx === structuredSteps.length - 1 ? 0.3 : 1,
                        border: "1px solid var(--line)",
                        borderRadius: "4px",
                        background: "none",
                      }}
                    >
                      ↓
                    </button>
                    <button
                      type="button"
                      data-testid={`step-delete-${idx}`}
                      title="Xóa bước"
                      aria-label={`Xóa bước ${idx + 1}`}
                      style={{
                        color: "var(--color-error)",
                        background: "none",
                        border: "none",
                        cursor: "pointer",
                        fontSize: "16px",
                        padding: "2px 6px",
                      }}
                      onClick={() => removeStepRow(idx)}
                    >
                      ×
                    </button>
                  </div>
                </div>

                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "1fr 140px",
                    gap: "8px",
                    alignItems: "center",
                  }}
                >
                  <input
                    id={`step-title-${idx}`}
                    data-testid={`step-title-${idx}`}
                    aria-label={`Tiêu đề bước ${idx + 1}`}
                    placeholder="Tiêu đề bước (vd: Sơ chế cá, Ướp gia vị...)"
                    value={st.title}
                    onChange={(e) => updateStepField(idx, "title", e.target.value)}
                    style={{ minHeight: "34px", padding: "4px 8px", fontSize: "12px" }}
                  />
                  <input
                    id={`step-duration-${idx}`}
                    data-testid={`step-duration-${idx}`}
                    type="number"
                    min="0"
                    aria-label={`Thời gian bước ${idx + 1} (phút)`}
                    placeholder="Thời gian (phút)"
                    value={st.durationMinutes ?? ""}
                    onChange={(e) =>
                      updateStepField(
                        idx,
                        "durationMinutes",
                        e.target.value ? parseInt(e.target.value, 10) : null,
                      )
                    }
                    style={{ minHeight: "34px", padding: "4px 8px", fontSize: "12px" }}
                  />
                </div>

                <textarea
                  id={`step-desc-${idx}`}
                  data-testid={`step-desc-${idx}`}
                  aria-label={`Nội dung bước ${idx + 1}`}
                  placeholder="Mô tả chi tiết các thao tác thực hiện trong bước này..."
                  required
                  rows={2}
                  value={st.description}
                  onChange={(e) => updateStepField(idx, "description", e.target.value)}
                  style={{ padding: "6px 8px", fontSize: "12px" }}
                />

                <input
                  id={`step-image-${idx}`}
                  data-testid={`step-image-${idx}`}
                  aria-label={`URL hình ảnh bước ${idx + 1}`}
                  placeholder="URL ảnh minh họa bước nấu (tùy chọn)"
                  value={st.imageUrl ?? ""}
                  onChange={(e) => updateStepField(idx, "imageUrl", e.target.value || null)}
                  style={{ minHeight: "32px", padding: "4px 8px", fontSize: "12px" }}
                />
              </div>
            ))}

            {structuredSteps.length === 0 && (
              <label className={styles.field} style={{ margin: 0 }}>
                Nhập nhanh các bước chế biến (mỗi bước một dòng)
                <textarea
                  id="steps"
                  aria-invalid={Boolean(fieldErrors.steps)}
                  aria-describedby={fieldErrors.steps ? "steps-error" : undefined}
                  value={steps}
                  onChange={(event) => {
                    setSteps(event.target.value);
                    clearFieldError("steps");
                  }}
                  placeholder={"Sơ chế nguyên liệu.\nNấu và nêm nếm.\nTrình bày ra đĩa."}
                />
              </label>
            )}
          </div>

          {/* Nutrition section */}
          <div
            style={{
              border: "1px solid var(--line)",
              borderRadius: "6px",
              padding: "16px",
              background: "var(--color-surface)",
            }}
          >
            <label
              style={{
                display: "flex",
                alignItems: "center",
                gap: "8px",
                fontWeight: 600,
                marginBottom: "8px",
                cursor: "pointer",
              }}
            >
              <input
                id="includeNutrition"
                type="checkbox"
                checked={includeNutrition}
                onChange={(e) => setIncludeNutrition(e.target.checked)}
              />
              Bao gồm thông tin dinh dưỡng (tính trên mỗi khẩu phần)
            </label>
            {includeNutrition && (
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "repeat(auto-fill, minmax(140px, 1fr))",
                  gap: "12px",
                  marginTop: "8px",
                }}
              >
                {(
                  [
                    { key: "calories", label: "Calories", unit: "kcal", step: 1 },
                    { key: "protein", label: "Protein", unit: "g", step: 0.1 },
                    { key: "carbohydrates", label: "Carbs", unit: "g", step: 0.1 },
                    { key: "fat", label: "Chất béo", unit: "g", step: 0.1 },
                    { key: "fiber", label: "Chất xơ", unit: "g", step: 0.1 },
                    { key: "sodium", label: "Natri", unit: "mg", step: 1 },
                  ] as const
                ).map(({ key, label, unit, step }) => (
                  <label
                    key={key}
                    style={{
                      display: "flex",
                      flexDirection: "column",
                      gap: "4px",
                      fontSize: "13px",
                    }}
                  >
                    <span>
                      {label}{" "}
                      <small style={{ color: "var(--color-muted)" }}>({unit})</small>
                    </span>
                    <input
                      id={`nutrition-${key}`}
                      type="number"
                      min="0"
                      step={step}
                      value={nutrition[key]}
                      onChange={(e) =>
                        setNutrition((prev) => ({
                          ...prev,
                          [key]: parseFloat(e.target.value) || 0,
                        }))
                      }
                      style={{ padding: "4px 8px", fontSize: "13px" }}
                    />
                  </label>
                ))}
              </div>
            )}
          </div>

          {/* Image Gallery section */}
          {editing && recipeId && (
            <div
              style={{
                border: "1px solid var(--line)",
                borderRadius: "6px",
                padding: "16px",
                background: "var(--color-surface)",
              }}
            >
              <span
                style={{
                  fontWeight: 600,
                  fontSize: "14px",
                  display: "block",
                  marginBottom: "12px",
                }}
              >
                Quản lý ảnh công thức
              </span>
              <div
                style={{ display: "flex", flexWrap: "wrap", gap: "8px", marginBottom: "12px" }}
              >
                {images.length === 0 && (
                  <span style={{ color: "var(--color-muted)", fontSize: "13px" }}>
                    Chưa có ảnh.
                  </span>
                )}
                {images.map((img) => (
                  <div
                    key={img.id}
                    data-testid={`image-card-${img.id}`}
                    style={{
                      position: "relative",
                      width: "100px",
                      border: img.isPrimary ? "2px solid var(--forest)" : "1px solid var(--line)",
                      borderRadius: "6px",
                      overflow: "hidden",
                    }}
                  >
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                      src={img.thumbnailUrl ?? img.originalUrl}
                      alt={img.altText ?? ""}
                      style={{ width: "100%", height: "80px", objectFit: "cover" }}
                    />
                    {img.isPrimary && (
                      <span
                        data-testid="badge-primary"
                        style={{
                          position: "absolute",
                          top: 2,
                          left: 2,
                          background: "var(--forest)",
                          color: "#fff",
                          fontSize: "9px",
                          padding: "2px 4px",
                          borderRadius: "3px",
                        }}
                      >
                        Ảnh chính
                      </span>
                    )}
                    <div style={{ display: "flex", gap: "2px", padding: "4px" }}>
                      {!img.isPrimary && (
                        <button
                          type="button"
                          data-testid={`btn-set-primary-${img.id}`}
                          title="Đặt làm ảnh chính"
                          style={{
                            flex: 1,
                            fontSize: "10px",
                            padding: "2px",
                            cursor: "pointer",
                            border: "1px solid var(--line)",
                            borderRadius: "3px",
                            background: "none",
                          }}
                          onClick={() => void handleSetPrimary(img.id)}
                        >
                          ⭐
                        </button>
                      )}
                      <button
                        type="button"
                        data-testid={`btn-delete-image-${img.id}`}
                        title="Xóa ảnh"
                        style={{
                          flex: 1,
                          fontSize: "10px",
                          padding: "2px",
                          cursor: "pointer",
                          border: "1px solid var(--color-error)",
                          borderRadius: "3px",
                          color: "var(--color-error)",
                          background: "none",
                        }}
                        onClick={() => void handleDeleteImage(img.id)}
                      >
                        🗑
                      </button>
                    </div>
                  </div>
                ))}
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                <input
                  id="imageAltText"
                  placeholder="Alt text (tùy chọn)"
                  value={imageAltText}
                  onChange={(e) => setImageAltText(e.target.value)}
                  style={{
                    padding: "6px 10px",
                    fontSize: "13px",
                    border: "1px solid var(--line)",
                    borderRadius: "4px",
                  }}
                />
                <label
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "6px",
                    fontSize: "13px",
                    cursor: "pointer",
                  }}
                >
                  <input
                    id="imageSetPrimary"
                    type="checkbox"
                    checked={imageSetPrimary}
                    onChange={(e) => setImageSetPrimary(e.target.checked)}
                  />
                  Đặt làm ảnh chính khi tải lên
                </label>
                <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
                  <input
                    ref={fileInputRef}
                    id="imageFile"
                    type="file"
                    accept=".jpg,.jpeg,.png,.webp,.avif"
                    disabled={uploadingImage || images.length >= 10}
                    onChange={(e) => {
                      const f = e.target.files?.[0];
                      if (f) void handleImageUpload(f);
                    }}
                    style={{ fontSize: "13px", flex: 1 }}
                  />
                  {uploadingImage && (
                    <span style={{ fontSize: "13px", color: "var(--color-muted)" }}>
                      Đang tải...
                    </span>
                  )}
                </div>
                {images.length >= 10 && (
                  <span style={{ fontSize: "12px", color: "var(--color-error)" }}>
                    Đã đạt giới hạn 10 ảnh.
                  </span>
                )}
              </div>
            </div>
          )}
          {!editing && (
            <p style={{ fontSize: "12px", color: "var(--color-muted)", fontStyle: "italic" }}>
              💡 Sau khi lưu bản nháp, bạn có thể quay lại để tải ảnh lên.
            </p>
          )}

          <div className={styles.actions}>
            <button className={styles.button} type="submit" disabled={saving || !categoryId}>
              {saving ? "Đang lưu..." : "Lưu bản nháp"}
            </button>
            <Link className={styles.buttonSecondary} href="/dashboard/recipes">
              Hủy
            </Link>
          </div>
        </form>
      )}
    </main>
  );
}
