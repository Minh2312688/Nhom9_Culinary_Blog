"use client";

import { type FormEvent, useCallback, useEffect, useState } from "react";
import { useParams } from "next/navigation";
import {
  addRecipeIngredient,
  addRecipeStep,
  ApiError,
  deleteRecipeIngredient,
  deleteRecipeStep,
  getRecipeDetail,
  RecipeDetail,
  RecipeIngredient,
  RecipeStep,
  updateRecipeIngredient,
  updateRecipeStep,
} from "@/lib/api";

type IngredientDraft = { name: string; quantity: string; unit: string; notes: string; orderIndex: string };
type StepDraft = { title: string; description: string; durationMinutes: string; imageUrl: string };

const blankIngredient = (): IngredientDraft => ({ name: "", quantity: "", unit: "", notes: "", orderIndex: "0" });
const blankStep = (): StepDraft => ({ title: "", description: "", durationMinutes: "", imageUrl: "" });
const fieldClass = "mt-1 w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-orange-500 focus:outline-none focus:ring-2 focus:ring-orange-200";
const buttonClass = "rounded-lg bg-orange-600 px-4 py-2 text-sm font-semibold text-white hover:bg-orange-700 disabled:cursor-not-allowed disabled:opacity-60";

export default function RecipeComponentsPage() {
  const params = useParams<{ slug: string }>();
  const slug = params.slug;
  const [recipe, setRecipe] = useState<RecipeDetail | null>(null);
  const [ingredientDraft, setIngredientDraft] = useState<IngredientDraft>(blankIngredient);
  const [stepDraft, setStepDraft] = useState<StepDraft>(blankStep);
  const [editingIngredient, setEditingIngredient] = useState<string | null>(null);
  const [editingStep, setEditingStep] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const loadRecipe = useCallback(async () => {
    const token = window.sessionStorage.getItem("culinary_access_token");
    if (!token) {
      setError("Phiên đăng nhập đã hết hoặc chưa đăng nhập. Hãy đăng nhập rồi thử lại.");
      setLoading(false);
      return;
    }
    try {
      const result = await getRecipeDetail(slug, token);
      setRecipe(result);
      setError(null);
    } catch (reason) {
      setError((reason as ApiError).detail || "Không tải được công thức.");
    } finally {
      setLoading(false);
    }
  }, [slug]);

  useEffect(() => { void loadRecipe(); }, [loadRecipe]);

  const tokenOrThrow = () => {
    const token = window.sessionStorage.getItem("culinary_access_token");
    if (!token) throw { detail: "Phiên đăng nhập đã hết hoặc chưa đăng nhập." } satisfies Partial<ApiError>;
    return token;
  };

  const runMutation = async (action: () => Promise<unknown>, successMessage: string): Promise<boolean> => {
    setSaving(true);
    setError(null);
    setNotice(null);
    try {
      await action();
      await loadRecipe();
      setNotice(successMessage);
      return true;
    } catch (reason) {
      setError((reason as ApiError).detail || "Không thể lưu thay đổi.");
      return false;
    } finally {
      setSaving(false);
    }
  };

  const submitIngredient = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!recipe) return;
    const payload = {
      name: ingredientDraft.name,
      quantity: ingredientDraft.quantity.trim() ? Number(ingredientDraft.quantity) : null,
      unit: ingredientDraft.unit.trim() || null,
      notes: ingredientDraft.notes.trim() || null,
      orderIndex: Number(ingredientDraft.orderIndex),
    };
    const saved = await runMutation(async () => {
      const token = tokenOrThrow();
      return editingIngredient
        ? updateRecipeIngredient(recipe.id, editingIngredient, payload, token)
        : addRecipeIngredient(recipe.id, payload, token);
    }, editingIngredient ? "Đã cập nhật nguyên liệu." : "Đã thêm nguyên liệu.");
    if (saved) {
      setEditingIngredient(null);
      setIngredientDraft(blankIngredient());
    }
  };

  const submitStep = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!recipe) return;
    const payload = {
      title: stepDraft.title,
      description: stepDraft.description,
      durationMinutes: stepDraft.durationMinutes.trim() ? Number(stepDraft.durationMinutes) : null,
      imageUrl: stepDraft.imageUrl.trim() || null,
    };
    const saved = await runMutation(async () => {
      const token = tokenOrThrow();
      return editingStep
        ? updateRecipeStep(recipe.id, editingStep, payload, token)
        : addRecipeStep(recipe.id, payload, token);
    }, editingStep ? "Đã cập nhật bước nấu." : "Đã thêm bước nấu.");
    if (saved) {
      setEditingStep(null);
      setStepDraft(blankStep());
    }
  };

  const editIngredient = (ingredient: RecipeIngredient) => {
    setEditingIngredient(ingredient.id);
    setIngredientDraft({
      name: ingredient.name,
      quantity: ingredient.quantity?.toString() ?? "",
      unit: ingredient.unit ?? "",
      notes: ingredient.notes ?? "",
      orderIndex: ingredient.orderIndex.toString(),
    });
  };

  const editStep = (step: RecipeStep) => {
    setEditingStep(step.id);
    setStepDraft({
      title: step.title ?? "",
      description: step.description,
      durationMinutes: step.durationMinutes?.toString() ?? "",
      imageUrl: step.imageUrl ?? "",
    });
  };

  const removeIngredient = (ingredient: RecipeIngredient) => {
    if (!recipe || !window.confirm(`Xóa nguyên liệu “${ingredient.name}”?`)) return;
    void runMutation(
      () => deleteRecipeIngredient(recipe.id, ingredient.id, tokenOrThrow()),
      "Đã xóa nguyên liệu.",
    );
  };

  const removeStep = (step: RecipeStep) => {
    if (!recipe || !window.confirm(`Xóa bước ${step.stepNumber}?`)) return;
    void runMutation(
      () => deleteRecipeStep(recipe.id, step.id, tokenOrThrow()),
      "Đã xóa bước và cập nhật thứ tự.",
    );
  };

  if (loading) return <main className="mx-auto max-w-5xl p-6" aria-live="polite">Đang tải công thức…</main>;
  if (!recipe) return <main className="mx-auto max-w-5xl p-6"><p role="alert" className="text-red-700">{error}</p></main>;

  return (
    <main className="mx-auto max-w-5xl space-y-8 px-4 py-8 sm:px-6">
      <header>
        <p className="text-sm font-semibold uppercase tracking-wide text-orange-700">Quản lý công thức</p>
        <h1 className="mt-2 text-3xl font-bold text-gray-900">{recipe.title}</h1>
        <p className="mt-2 text-sm text-gray-600">Chỉnh sửa nguyên liệu và các bước nấu. Số thứ tự bước do máy chủ quản lý.</p>
      </header>

      {error && <p role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</p>}
      {notice && <p role="status" className="rounded-lg border border-green-200 bg-green-50 p-3 text-sm text-green-800">{notice}</p>}

      <section className="grid gap-6 lg:grid-cols-2">
        <div className="rounded-2xl border border-gray-200 bg-white p-5 shadow-sm sm:p-6">
          <h2 className="text-xl font-bold">Nguyên liệu ({recipe.ingredients.length})</h2>
          <ul className="mt-4 divide-y divide-gray-100">
            {recipe.ingredients.map((ingredient) => (
              <li key={ingredient.id} className="flex items-start justify-between gap-3 py-3">
                <div>
                  <p className="font-medium">{ingredient.name}</p>
                  <p className="text-sm text-gray-600">{ingredient.quantity ?? "Vừa đủ"} {ingredient.unit ?? ""}{ingredient.notes ? ` · ${ingredient.notes}` : ""}</p>
                  <p className="text-xs text-gray-500">Thứ tự: {ingredient.orderIndex}</p>
                </div>
                <div className="flex shrink-0 gap-2">
                  <button type="button" onClick={() => editIngredient(ingredient)} className="text-sm font-medium text-orange-700 hover:underline">Sửa</button>
                  <button type="button" onClick={() => removeIngredient(ingredient)} disabled={saving} className="text-sm font-medium text-red-700 hover:underline disabled:opacity-50">Xóa</button>
                </div>
              </li>
            ))}
            {recipe.ingredients.length === 0 && <li className="py-4 text-sm text-gray-500">Chưa có nguyên liệu.</li>}
          </ul>

          <form onSubmit={submitIngredient} className="mt-5 space-y-3 border-t border-gray-100 pt-5">
            <h3 className="font-semibold">{editingIngredient ? "Sửa nguyên liệu" : "Thêm nguyên liệu"}</h3>
            <label className="block text-sm font-medium">Tên nguyên liệu<input required maxLength={200} value={ingredientDraft.name} onChange={(e) => setIngredientDraft({ ...ingredientDraft, name: e.target.value })} className={fieldClass} /></label>
            <div className="grid grid-cols-2 gap-3">
              <label className="block text-sm font-medium">Số lượng<input type="number" min="0.001" max="9999999.999" step="0.001" value={ingredientDraft.quantity} onChange={(e) => setIngredientDraft({ ...ingredientDraft, quantity: e.target.value })} className={fieldClass} /></label>
              <label className="block text-sm font-medium">Đơn vị<input maxLength={50} value={ingredientDraft.unit} onChange={(e) => setIngredientDraft({ ...ingredientDraft, unit: e.target.value })} className={fieldClass} /></label>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <label className="block text-sm font-medium">Ghi chú<input maxLength={500} value={ingredientDraft.notes} onChange={(e) => setIngredientDraft({ ...ingredientDraft, notes: e.target.value })} className={fieldClass} /></label>
              <label className="block text-sm font-medium">Thứ tự<input required type="number" min="0" step="1" value={ingredientDraft.orderIndex} onChange={(e) => setIngredientDraft({ ...ingredientDraft, orderIndex: e.target.value })} className={fieldClass} /></label>
            </div>
            <div className="flex gap-3">
              <button disabled={saving} className={buttonClass}>{saving ? "Đang lưu…" : editingIngredient ? "Lưu nguyên liệu" : "Thêm nguyên liệu"}</button>
              {editingIngredient && <button type="button" onClick={() => { setEditingIngredient(null); setIngredientDraft(blankIngredient()); }} className="rounded-lg border border-gray-300 px-4 py-2 text-sm">Hủy sửa</button>}
            </div>
          </form>
        </div>

        <div className="rounded-2xl border border-gray-200 bg-white p-5 shadow-sm sm:p-6">
          <h2 className="text-xl font-bold">Các bước nấu ({recipe.steps.length})</h2>
          <ol className="mt-4 divide-y divide-gray-100">
            {recipe.steps.map((step) => (
              <li key={step.id} className="flex items-start justify-between gap-3 py-3">
                <div>
                  <p className="font-medium">Bước {step.stepNumber}: {step.title ?? "Chưa đặt tiêu đề"}</p>
                  <p className="text-sm text-gray-600">{step.description}</p>
                  {step.durationMinutes !== null && <p className="text-xs text-gray-500">{step.durationMinutes} phút</p>}
                </div>
                <div className="flex shrink-0 gap-2">
                  <button type="button" onClick={() => editStep(step)} className="text-sm font-medium text-orange-700 hover:underline">Sửa</button>
                  <button type="button" onClick={() => removeStep(step)} disabled={saving} className="text-sm font-medium text-red-700 hover:underline disabled:opacity-50">Xóa</button>
                </div>
              </li>
            ))}
            {recipe.steps.length === 0 && <li className="py-4 text-sm text-gray-500">Chưa có bước nấu.</li>}
          </ol>

          <form onSubmit={submitStep} className="mt-5 space-y-3 border-t border-gray-100 pt-5">
            <h3 className="font-semibold">{editingStep ? "Sửa bước nấu" : "Thêm bước nấu"}</h3>
            <label className="block text-sm font-medium">Tiêu đề<input required maxLength={200} value={stepDraft.title} onChange={(e) => setStepDraft({ ...stepDraft, title: e.target.value })} className={fieldClass} /></label>
            <label className="block text-sm font-medium">Mô tả<textarea required rows={3} value={stepDraft.description} onChange={(e) => setStepDraft({ ...stepDraft, description: e.target.value })} className={fieldClass} /></label>
            <div className="grid grid-cols-2 gap-3">
              <label className="block text-sm font-medium">Thời lượng (phút)<input type="number" min="0" step="1" value={stepDraft.durationMinutes} onChange={(e) => setStepDraft({ ...stepDraft, durationMinutes: e.target.value })} className={fieldClass} /></label>
              <label className="block text-sm font-medium">URL hình ảnh<input type="url" maxLength={500} value={stepDraft.imageUrl} onChange={(e) => setStepDraft({ ...stepDraft, imageUrl: e.target.value })} className={fieldClass} /></label>
            </div>
            <div className="flex gap-3">
              <button disabled={saving} className={buttonClass}>{saving ? "Đang lưu…" : editingStep ? "Lưu bước nấu" : "Thêm bước nấu"}</button>
              {editingStep && <button type="button" onClick={() => { setEditingStep(null); setStepDraft(blankStep()); }} className="rounded-lg border border-gray-300 px-4 py-2 text-sm">Hủy sửa</button>}
            </div>
          </form>
        </div>
      </section>
    </main>
  );
}
