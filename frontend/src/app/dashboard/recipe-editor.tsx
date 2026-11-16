"use client";

import Link from "next/link";
import { useEffect, useRef, useState, type ChangeEvent } from "react";
import { useParams, useRouter } from "next/navigation";
import { zodResolver } from "@hookform/resolvers/zod";
import { useFieldArray, useForm } from "react-hook-form";
import { z } from "zod";
import {
  createRecipe,
  getPrivateRecipe,
  getRecipeCategories,
  updateRecipe,
  uploadRecipeImage,
  uploadRecipeStepImage,
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

export interface StructuredStep {
  id?: string;
  stepNumber?: number;
  title: string;
  description: string;
  durationMinutes: number | null;
  imageUrl: string | null;
  imageFile?: File | null;
  imagePreviewUrl?: string | null;
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
      imageFile: null,
      imagePreviewUrl: null,
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

interface PendingGalleryImage {
  id: string;
  file: File;
  previewUrl: string;
  altText: string | null;
}

type RecipeWizardStep = "general" | "ingredients" | "steps" | "review";

const wizardSteps: { id: RecipeWizardStep; label: string }[] = [
  { id: "general", label: "Thông tin chung" },
  { id: "ingredients", label: "Nguyên liệu" },
  { id: "steps", label: "Các bước chế biến" },
  { id: "review", label: "Xem lại" },
];

const generalFields = [
  "title",
  "description",
  "categoryId",
  "prepTime",
  "cookTime",
  "servings",
  "difficulty",
] as const;
const ingredientFormFields = ["ingredients"] as const;

const recipeEditorSchema = z.object({
  title: z.string().trim().min(5, "Tiêu đề phải có ít nhất 5 ký tự.").max(200, "Tiêu đề không được vượt quá 200 ký tự."),
  description: z.string().trim(),
  categoryId: z.string().min(1, "Vui lòng chọn danh mục."),
  prepTime: z.string().regex(/^\d+$/, "Thời gian chuẩn bị phải là số nguyên không âm."),
  cookTime: z.string().regex(/^\d+$/, "Thời gian nấu phải là số nguyên không âm."),
  servings: z.string().regex(/^[1-9]\d*$/, "Khẩu phần phải là số nguyên lớn hơn 0."),
  difficulty: z.string().refine(
    (value) => ["Easy", "Medium", "Hard", "Expert"].includes(value),
    "Vui lòng chọn độ khó hợp lệ.",
  ),
  ingredients: z.array(z.object({
    name: z.string().trim().min(1, "Vui lòng nhập tên nguyên liệu."),
    quantity: z.string().trim().refine(
      (value) => value === "" || (Number.isFinite(Number(value)) && Number(value) >= 0),
      "Số lượng phải là số không âm.",
    ),
    unit: z.string(),
    notes: z.string(),
  })),
  steps: z.string(),
});

type RecipeEditorFormValues = z.infer<typeof recipeEditorSchema>;

export function RecipeEditor({ editing = false }: { editing?: boolean }) {
  const router = useRouter();
  const { showToast } = useFeedback();
  const params = useParams<{ id?: string }>();
  const recipeId = params?.id;
  const {
    control,
    register,
    handleSubmit,
    reset,
    setValue,
    setError: setFormError,
    watch,
    clearErrors,
    trigger,
    formState: { errors },
  } = useForm<RecipeEditorFormValues>({
    resolver: zodResolver(recipeEditorSchema),
    mode: "onBlur",
    defaultValues: {
      title: "",
      description: "",
      categoryId: "",
      prepTime: "0",
      cookTime: "0",
      servings: "2",
      difficulty: "Easy",
      ingredients: [],
      steps: "",
    },
  });
  const {
    fields: ingredientFields,
    append: appendIngredient,
    remove: removeIngredient,
  } = useFieldArray({ control, name: "ingredients" });
  const [categories, setCategories] = useState<RecipeCategory[]>([]);
  const [recipe, setRecipe] = useState<RecipeDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [isConflict, setIsConflict] = useState(false);
  const [activeStep, setActiveStep] = useState<RecipeWizardStep>("general");
  const activeStepIndex = wizardSteps.findIndex((step) => step.id === activeStep);

  const steps = watch("steps");
  const title = watch("title");
  const categoryId = watch("categoryId");
  const formValues = watch();
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
  const [pendingGalleryImages, setPendingGalleryImages] = useState<PendingGalleryImage[]>([]);
  const [pendingImageDeletes, setPendingImageDeletes] = useState<string[]>([]);
  const [pendingPrimaryImageId, setPendingPrimaryImageId] = useState<string | null>(null);
  const [imageAltText, setImageAltText] = useState("");
  const [imageSetPrimary, setImageSetPrimary] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);
  const [pendingImageFile, setPendingImageFile] = useState<File | null>(null);
  const [pendingImagePreviewUrl, setPendingImagePreviewUrl] = useState<string | null>(null);
  const [createdRecipeId, setCreatedRecipeId] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const galleryPreviewUrlsRef = useRef(new Set<string>());
  const stepPreviewUrlsRef = useRef(new Set<string>());
  const visibleImages = [
    ...images.filter((image) => !pendingImageDeletes.includes(image.id)),
    ...pendingGalleryImages.map((image, index) => ({
      id: image.id,
      originalUrl: image.previewUrl,
      thumbnailUrl: image.previewUrl,
      altText: image.altText,
      isPrimary: false,
      orderIndex: images.length + index,
    })),
  ].map((image) => ({ ...image, isPrimary: image.id === pendingPrimaryImageId }));
  const hasPendingGalleryChanges = pendingGalleryImages.length > 0
    || pendingImageDeletes.length > 0
    || pendingPrimaryImageId !== (images.find((image) => image.isPrimary)?.id ?? null);
  const hasPendingImageUploads = Boolean(
    pendingImageFile || structuredSteps.some((step) => step.imageFile),
  );
  const reviewSteps = structuredSteps.length > 0
    ? structuredSteps
    : parseStepsText(formValues.steps);
  const reviewImageUrl = pendingImagePreviewUrl
    ?? visibleImages.find((image) => image.isPrimary)?.originalUrl
    ?? visibleImages[0]?.originalUrl
    ?? null;

  useEffect(() => {
    if (!pendingImageFile) {
      setPendingImagePreviewUrl(null);
      return;
    }

    const url = URL.createObjectURL(pendingImageFile);
    setPendingImagePreviewUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [pendingImageFile]);

  useEffect(() => {
    const currentUrls = new Set(pendingGalleryImages.map((image) => image.previewUrl));
    for (const url of galleryPreviewUrlsRef.current) {
      if (!currentUrls.has(url)) URL.revokeObjectURL(url);
    }
    galleryPreviewUrlsRef.current = currentUrls;
  }, [pendingGalleryImages]);

  useEffect(() => {
    const currentUrls = new Set(
      structuredSteps.flatMap((step) => step.imagePreviewUrl ? [step.imagePreviewUrl] : []),
    );
    for (const url of stepPreviewUrlsRef.current) {
      if (!currentUrls.has(url)) URL.revokeObjectURL(url);
    }
    stepPreviewUrlsRef.current = currentUrls;
  }, [structuredSteps]);

  useEffect(() => () => {
    for (const url of galleryPreviewUrlsRef.current) URL.revokeObjectURL(url);
    for (const url of stepPreviewUrlsRef.current) URL.revokeObjectURL(url);
  }, []);

  async function loadRecipeData(signal?: AbortSignal) {
    const categoryItems = await getRecipeCategories(signal ?? new AbortController().signal);
    setCategories(categoryItems);
    if (editing && recipeId) {
      const detail = await getPrivateRecipe(recipeId, signal);
      setRecipe(detail);
      reset({
        title: detail.title,
        description: detail.description ?? "",
        categoryId: detail.categoryId,
        prepTime: String(detail.prepTimeMinutes),
        cookTime: String(detail.cookTimeMinutes),
        servings: String(detail.servings),
        difficulty: detail.difficulty,
        ingredients: detail.ingredients
          .slice()
          .sort((first, second) => first.orderIndex - second.orderIndex)
          .map((item) => ({
            name: item.name,
            quantity: item.quantity === null ? "" : String(item.quantity),
            unit: item.unit ?? "",
            notes: item.notes ?? "",
          })),
        steps: "",
      });

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
            imageFile: null,
            imagePreviewUrl: null,
          })),
        );
        setValue("steps", sorted.map((item) => item.description).join("\n"));
      } else {
        setStructuredSteps([]);
        setValue("steps", "");
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
      setPendingGalleryImages([]);
      setPendingImageDeletes([]);
      setPendingPrimaryImageId(detail.images.find((image) => image.isPrimary)?.id ?? null);
    } else if (categoryItems.length) {
      setValue("categoryId", categoryItems[0].id);
    }
  }

  useEffect(() => {
    if (editing && !recipeId) {
      setError("Không thể xác định công thức cần chỉnh sửa.");
      setLoading(false);
      return;
    }

    const controller = new AbortController();
    async function load() {
      try {
        await loadRecipeData(controller.signal);
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
    void load();
    return () => controller.abort();
  }, [editing, recipeId, router, showToast]);

  async function reloadRecipe() {
    setLoading(true);
    setIsConflict(false);
    setError(null);
    setFieldErrors({});
    try {
      await loadRecipeData();
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
    switch (field) {
      case "title":
      case "description":
      case "categoryId":
      case "difficulty":
      case "steps":
        clearErrors(field);
        break;
      case "ingredients":
        clearErrors("ingredients");
        break;
      case "prepTimeMinutes":
        clearErrors("prepTime");
        break;
      case "cookTimeMinutes":
        clearErrors("cookTime");
        break;
      case "servings":
        clearErrors("servings");
        break;
    }
    setError(null);
  }

  function addIngredientRow() {
    appendIngredient({ name: "", quantity: "", unit: "", notes: "" });
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
        imageFile: null,
        imagePreviewUrl: null,
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

  function handleStepImageSelection(index: number, event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (file && file.size > 5 * 1024 * 1024) {
      event.target.value = "";
      setError("Ảnh bước nấu không được vượt quá 5 MB.");
      showToast("Ảnh bước nấu không được vượt quá 5 MB.", "error");
      return;
    }
    const imagePreviewUrl = file ? URL.createObjectURL(file) : null;
    setStructuredSteps((current) => current.map((step, stepIndex) =>
      stepIndex === index
        ? { ...step, imageFile: file ?? null, imagePreviewUrl }
        : step,
    ));
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

  async function advanceWizard() {
    if (activeStep === "general") {
      if (await trigger(generalFields, { shouldFocus: true })) {
        setActiveStep("ingredients");
      }
      return;
    }

    if (activeStep === "ingredients") {
      if (await trigger(ingredientFormFields, { shouldFocus: true })) {
        setActiveStep("steps");
      }
      return;
    }

    if (activeStep === "steps") {
      const invalidIndex = structuredSteps.findIndex((step) => !step.description.trim());
      if (invalidIndex >= 0) {
        setFormError("steps", {
          type: "manual",
          message: "Vui lòng nhập mô tả cho từng bước.",
        });
        document.getElementById(`step-desc-${invalidIndex}`)?.focus();
        return;
      }

      const parsedSteps = parseStepsText(steps);
      if (parsedSteps.some((step) => !step.description.trim())) {
        setFormError("steps", {
          type: "manual",
          message: "Vui lòng nhập mô tả cho từng bước.",
        });
        document.getElementById("steps")?.focus();
        return;
      }
      setActiveStep("review");
    }
  }

  function goToPreviousStep() {
    const index = wizardSteps.findIndex((step) => step.id === activeStep);
    if (index > 0) setActiveStep(wizardSteps[index - 1].id);
  }

  function handlePendingGalleryImageSelection(file: File | undefined) {
    if (!file) return;
    if (file.size > 5 * 1024 * 1024) {
      showToast("File quá lớn (tối đa 5MB).", "error");
      if (fileInputRef.current) fileInputRef.current.value = "";
      return;
    }
    if (visibleImages.length >= 10) {
      showToast("Đã đạt giới hạn 10 ảnh.", "error");
      if (fileInputRef.current) fileInputRef.current.value = "";
      return;
    }
    const pendingImage: PendingGalleryImage = {
      id: `pending-${crypto.randomUUID()}`,
      file,
      previewUrl: URL.createObjectURL(file),
      altText: imageAltText.trim() || null,
    };
    const makePrimary = imageSetPrimary || visibleImages.length === 0;
    setPendingGalleryImages((current) => [...current, pendingImage]);
    if (makePrimary) setPendingPrimaryImageId(pendingImage.id);
    setImageAltText("");
    setImageSetPrimary(false);
    if (fileInputRef.current) fileInputRef.current.value = "";
  }

  async function uploadPendingImage(targetRecipeId: string) {
    if (!pendingImageFile) return;

    setUploadingImage(true);
    try {
      const result = await uploadRecipeImage(
        targetRecipeId,
        pendingImageFile,
        imageSetPrimary,
        imageAltText || undefined,
      );
      setImages((current) => [...current, {
        id: result.imageId,
        originalUrl: result.originalUrl,
        thumbnailUrl: result.thumbnailUrl,
        altText: result.altText,
        isPrimary: result.isPrimary,
        orderIndex: current.length,
      }]);
      setPendingImageFile(null);
      setImageAltText("");
      setImageSetPrimary(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    } finally {
      setUploadingImage(false);
    }
  }

  function handlePendingImageSelection(file: File | undefined) {
    if (!file) {
      setPendingImageFile(null);
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      showToast("File quá lớn (tối đa 5MB).", "error");
      if (fileInputRef.current) fileInputRef.current.value = "";
      setPendingImageFile(null);
      return;
    }
    setPendingImageFile(file);
    setError(null);
  }

  async function applyPendingGalleryChanges(targetRecipeId: string): Promise<boolean> {
    const deletesLastImage = pendingImageDeletes.length > 0
      && pendingImageDeletes.length === images.length
      && pendingGalleryImages.length === 0;
    let primaryImageId = pendingPrimaryImageId;
    let currentImages = [...images];

    for (const pendingImage of pendingGalleryImages) {
      setUploadingImage(true);
      try {
        const result = await uploadRecipeImage(
          targetRecipeId,
          pendingImage.file,
          false,
          pendingImage.altText ?? undefined,
        );
        if (primaryImageId === pendingImage.id) {
          primaryImageId = result.imageId;
          setPendingPrimaryImageId(result.imageId);
        }
        const uploadedImage: ImageEntry = {
          id: result.imageId,
          originalUrl: result.originalUrl,
          thumbnailUrl: result.thumbnailUrl,
          altText: result.altText,
          isPrimary: result.isPrimary,
          orderIndex: currentImages.length,
        };
        currentImages = result.isPrimary
          ? currentImages.map((image) => ({ ...image, isPrimary: false })).concat(uploadedImage)
          : [...currentImages, uploadedImage];
        setImages(currentImages);
        setPendingGalleryImages((current) =>
          current.filter((image) => image.id !== pendingImage.id),
        );
      } finally {
        setUploadingImage(false);
      }
    }

    for (const imageId of pendingImageDeletes) {
      await deleteRecipeImage(targetRecipeId, imageId);
      const deletedImage = currentImages.find((image) => image.id === imageId);
      currentImages = currentImages.filter((image) => image.id !== imageId);
      if (deletedImage?.isPrimary && currentImages.length > 0) {
        currentImages = currentImages.map((image, index) => ({
          ...image,
          isPrimary: index === 0,
        }));
      }
      setImages(currentImages);
      setPendingImageDeletes((current) => current.filter((id) => id !== imageId));
    }

    const currentPrimaryImageId = currentImages.find((image) => image.isPrimary)?.id ?? null;
    if (primaryImageId && primaryImageId !== currentPrimaryImageId) {
      await setPrimaryRecipeImage(targetRecipeId, primaryImageId);
      currentImages = currentImages.map((image) => ({
        ...image,
        isPrimary: image.id === primaryImageId,
      }));
      setImages(currentImages);
    }
    setPendingPrimaryImageId(primaryImageId);
    return deletesLastImage;
  }

  async function uploadPendingStepImages(targetRecipeId: string, savedSteps?: RecipeStep[]) {
    for (let index = 0; index < structuredSteps.length; index += 1) {
      const step = structuredSteps[index];
      if (!step.imageFile) continue;

      const stepId = step.id ?? savedSteps?.[index]?.id;
      if (!stepId) {
        throw new Error(`Không xác định được bước ${index + 1} để tải ảnh lên.`);
      }

      const updatedStep = await uploadRecipeStepImage(targetRecipeId, stepId, step.imageFile);
      setStructuredSteps((current) => current.map((item, itemIndex) =>
        itemIndex === index
          ? {
            ...item,
            id: stepId,
            imageUrl: updatedStep.imageUrl,
            imageFile: null,
            imagePreviewUrl: null,
          }
          : item,
      ));
    }
  }

  async function retryPendingImageUpload() {
    if (!createdRecipeId) return;
    setSaving(true);
    setError(null);
    try {
      if (pendingImageFile) await uploadPendingImage(createdRecipeId);
      await uploadPendingStepImages(createdRecipeId);
      setCreatedRecipeId(null);
      showToast("Ảnh đã được tải lên.", "success");
      router.push("/dashboard/recipes");
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Không thể tải ảnh.";
      setError(`Công thức đã lưu, nhưng ảnh chưa tải lên. ${message}`);
      showToast(message, "error");
    } finally {
      setSaving(false);
    }
  }

  function handleSetPrimary(imageId: string) {
    setPendingPrimaryImageId(imageId);
  }

  async function handleDeleteImage(imageId: string) {
    if (!recipeId || !confirm("Đánh dấu ảnh này để xóa khi cập nhật công thức?")) return;
    const remainingImages = visibleImages.filter((image) => image.id !== imageId);
    setPendingGalleryImages((current) => current.filter((image) => image.id !== imageId));
    if (images.some((image) => image.id === imageId)) {
      setPendingImageDeletes((current) =>
        current.includes(imageId) ? current : [...current, imageId],
      );
    }
    if (pendingPrimaryImageId === imageId) {
      setPendingPrimaryImageId(remainingImages[0]?.id ?? null);
    }
  }

  async function onSubmit(values: RecipeEditorFormValues) {
    if (editing && (!recipe || !recipeId)) {
      const message = error ?? "Không thể lưu khi chưa tải được công thức cần chỉnh sửa.";
      setError(message);
      showToast(message, "error");
      return;
    }

    setSaving(true);
    setError(null);
    setFieldErrors({});
    setIsConflict(false);

    if (createdRecipeId) {
      await retryPendingImageUpload();
      return;
    }

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
        : values.steps
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
      title: values.title,
      description: values.description || null,
      categoryId: values.categoryId,
      prepTimeMinutes: Number(values.prepTime),
      cookTimeMinutes: Number(values.cookTime),
      servings: Number(values.servings),
      difficulty: values.difficulty,
      ingredients: values.ingredients
        .filter((ingredient) => ingredient.name.trim())
        .map((ingredient, orderIndex) => ({
          name: ingredient.name.trim(),
          quantity: ingredient.quantity ? Number(ingredient.quantity) : null,
          unit: ingredient.unit.trim() || null,
          notes: ingredient.notes.trim() || null,
          orderIndex,
        })),
      steps: resolvedSteps,
      nutrition: includeNutrition ? nutrition : null,
    };

    try {
      let savedRecipe: RecipeDetail;
      if (editing && recipe && recipeId) {
        savedRecipe = await updateRecipe(recipeId, { ...payload, rowVersion: recipe.rowVersion });
      } else {
        savedRecipe = await createRecipe(payload);
      }
      if (editing) setRecipe(savedRecipe);

      setStructuredSteps((current) => current.map((step, index) => ({
        ...step,
        id: savedRecipe.steps[index]?.id ?? step.id,
      })));
      let deletedLastImage = false;
      if (editing && hasPendingGalleryChanges) {
        try {
          deletedLastImage = await applyPendingGalleryChanges(savedRecipe.id);
        } catch (reason) {
          const message = reason instanceof Error ? reason.message : "Không thể cập nhật ảnh.";
          setError(`Công thức đã được cập nhật, nhưng thay đổi ảnh chưa hoàn tất. ${message}`);
          showToast(message, "error");
          setSaving(false);
          return;
        }
      }
      if (!editing && hasPendingImageUploads) {
        setCreatedRecipeId(savedRecipe.id);
        try {
          if (pendingImageFile) await uploadPendingImage(savedRecipe.id);
          await uploadPendingStepImages(savedRecipe.id, savedRecipe.steps);
          setCreatedRecipeId(null);
        } catch (reason) {
          const message = reason instanceof Error ? reason.message : "Không thể tải ảnh.";
          setError(`Công thức đã lưu, nhưng ảnh chưa tải lên. ${message}`);
          showToast(message, "error");
          setSaving(false);
          return;
        }
      }
      showToast(
        deletedLastImage
          ? "Đã xóa ảnh cuối; công thức hiện không có ảnh."
          : editing && hasPendingGalleryChanges
            ? "Đã cập nhật công thức và ảnh."
            : editing
              ? "Đã cập nhật công thức."
              : "Đã lưu bản nháp.",
        "success",
      );
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
      const invalidFields = Object.keys(details.fieldErrors);
      if (invalidFields.some((field) => field === "ingredients" || field.startsWith("ingredients."))) {
        setActiveStep("ingredients");
      } else if (invalidFields.includes("steps")) {
        setActiveStep("steps");
      } else if (invalidFields.length > 0) {
        setActiveStep("general");
      }
      showToast(reason instanceof Error ? reason.message : "Không thể lưu công thức.", "error");
      setSaving(false);
    }
  }

  function renderFieldError(
    field: keyof RecipeEditorFormValues | "prepTimeMinutes" | "cookTimeMinutes",
  ) {
    const formField = field === "prepTimeMinutes" ? "prepTime"
      : field === "cookTimeMinutes" ? "cookTime"
      : field;
    const message = fieldErrors[field]?.join(" ") ?? errors[formField]?.message;
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
      {!loading && (!editing || recipe !== null) && (
        <form className={styles.recipeEditorForm} onSubmit={handleSubmit(onSubmit)} noValidate>
          {error && !isConflict && <ErrorNotice><span data-error-summary>{error}</span></ErrorNotice>}

          <nav className={styles.recipeWizardProgress} aria-label="Tiến trình tạo công thức">
            <p className={styles.recipeWizardProgressLabel} aria-live="polite">
              Bước {activeStepIndex + 1} / {wizardSteps.length}: {wizardSteps[activeStepIndex].label}
            </p>
            <div
              className={styles.recipeWizardTrack}
              role="progressbar"
              aria-label="Tiến độ công thức"
              aria-valuemin={0}
              aria-valuemax={wizardSteps.length}
              aria-valuenow={activeStepIndex + 1}
            >
              <span
                className={styles.recipeWizardFill}
                style={{ width: `${((activeStepIndex + 1) / wizardSteps.length) * 100}%` }}
              />
            </div>
            <ol className={styles.recipeWizardSteps}>
              {wizardSteps.map((step, index) => (
                <li
                  key={step.id}
                  aria-current={activeStep === step.id ? "step" : undefined}
                  data-completed={index < activeStepIndex ? "true" : undefined}
                  className={styles.recipeWizardStep}
                  data-testid={`recipe-wizard-step-${step.id}`}
                >
                  <span>{index + 1}. {step.label}</span>
                  {index < activeStepIndex && (
                    <span className={styles.recipeWizardStepStatus}>Đã hoàn thành</span>
                  )}
                </li>
              ))}
            </ol>
          </nav>

          {activeStep === "general" && (
            <section aria-label="Thông tin chung" style={{ display: "grid", gap: "16px" }}>
          <label className={styles.field}>
            Tiêu đề
            <input
              id="title"
              aria-invalid={Boolean(fieldErrors.title || errors.title)}
              aria-describedby={fieldErrors.title || errors.title ? "title-error" : undefined}
              required
              minLength={5}
              maxLength={200}
              {...register("title", { onChange: () => clearFieldError("title") })}
            />
            {renderFieldError("title")}
          </label>
          <label className={styles.field}>
            Mô tả
            <textarea
              id="description"
              aria-invalid={Boolean(fieldErrors.description || errors.description)}
              aria-describedby={fieldErrors.description || errors.description ? "description-error" : undefined}
              {...register("description", { onChange: () => clearFieldError("description") })}
            />
            {renderFieldError("description")}
          </label>
          <div className={styles.formGrid}>
            <label className={styles.field}>
              Danh mục
              <select
                id="categoryId"
                aria-invalid={Boolean(fieldErrors.categoryId || errors.categoryId)}
                aria-describedby={fieldErrors.categoryId || errors.categoryId ? "categoryId-error" : undefined}
                required
                {...register("categoryId", { onChange: () => clearFieldError("categoryId") })}
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
                aria-invalid={Boolean(fieldErrors.difficulty || errors.difficulty)}
                aria-describedby={fieldErrors.difficulty || errors.difficulty ? "difficulty-error" : undefined}
                {...register("difficulty", { onChange: () => clearFieldError("difficulty") })}
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
                aria-invalid={Boolean(fieldErrors.prepTimeMinutes || errors.prepTime)}
                aria-describedby={fieldErrors.prepTimeMinutes || errors.prepTime ? "prepTimeMinutes-error" : undefined}
                type="number"
                min="0"
                required
                {...register("prepTime", { onChange: () => clearFieldError("prepTimeMinutes") })}
              />
              {renderFieldError("prepTimeMinutes")}
            </label>
            <label className={styles.field}>
              Thời gian nấu (phút)
              <input
                id="cookTimeMinutes"
                aria-invalid={Boolean(fieldErrors.cookTimeMinutes || errors.cookTime)}
                aria-describedby={fieldErrors.cookTimeMinutes || errors.cookTime ? "cookTimeMinutes-error" : undefined}
                type="number"
                min="0"
                required
                {...register("cookTime", { onChange: () => clearFieldError("cookTimeMinutes") })}
              />
              {renderFieldError("cookTimeMinutes")}
            </label>
            <label className={styles.field}>
              Khẩu phần
              <input
                id="servings"
                aria-invalid={Boolean(fieldErrors.servings || errors.servings)}
                aria-describedby={fieldErrors.servings || errors.servings ? "servings-error" : undefined}
                type="number"
                min="1"
                required
                {...register("servings", { onChange: () => clearFieldError("servings") })}
              />
              {renderFieldError("servings")}
            </label>
          </div>
            </section>
          )}

          {activeStep === "ingredients" && (
          <section aria-labelledby="ingredients-heading">
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: "12px" }}>
              <h2 id="ingredients-heading" style={{ fontSize: "16px" }}>Nguyên liệu</h2>
              <button
                type="button"
                className={`${styles.buttonSecondary} ${styles.prominentActionButton}`}
                onClick={addIngredientRow}
              >
                Thêm nguyên liệu
              </button>
            </div>
            {ingredientFields.length === 0 && (
              <p className={styles.notice}>Chưa có nguyên liệu. Thêm dòng để bắt đầu.</p>
            )}
            <div
              className={styles.ingredientRows}
              aria-invalid={Boolean(fieldErrors.ingredients || errors.ingredients)}
              aria-describedby={fieldErrors.ingredients || errors.ingredients ? "ingredients-error" : undefined}
            >
              {ingredientFields.map((field, index) => (
                <div
                  key={field.id}
                  data-testid={`ingredient-row-${index}`}
                  className={styles.ingredientRow}
                >
                  <div className={styles.ingredientField}>
                    <input
                      className={styles.ingredientInput}
                      aria-label={`Tên nguyên liệu ${index + 1}`}
                      aria-invalid={Boolean(errors.ingredients?.[index]?.name)}
                      aria-describedby={errors.ingredients?.[index]?.name ? `ingredient-name-${index}-error` : undefined}
                      placeholder="Tên nguyên liệu"
                      {...register(`ingredients.${index}.name`, {
                        onChange: () => clearFieldError("ingredients"),
                      })}
                    />
                    {errors.ingredients?.[index]?.name?.message && (
                      <span
                        className={styles.fieldError}
                        id={`ingredient-name-${index}-error`}
                        role="alert"
                      >
                        {errors.ingredients[index]?.name?.message}
                      </span>
                    )}
                  </div>
                  <div className={styles.ingredientField}>
                    <input
                      className={styles.ingredientInput}
                      aria-label={`Số lượng nguyên liệu ${index + 1}`}
                      placeholder="Số lượng"
                      inputMode="decimal"
                      {...register(`ingredients.${index}.quantity`, {
                        onChange: () => clearFieldError("ingredients"),
                      })}
                    />
                  </div>
                  <div className={styles.ingredientField}>
                    <input
                      className={styles.ingredientInput}
                      aria-label={`Đơn vị nguyên liệu ${index + 1}`}
                      placeholder="Đơn vị"
                      {...register(`ingredients.${index}.unit`)}
                    />
                  </div>
                  <div className={styles.ingredientField}>
                    <input
                      className={styles.ingredientInput}
                      aria-label={`Ghi chú nguyên liệu ${index + 1}`}
                      placeholder="Ghi chú"
                      {...register(`ingredients.${index}.notes`)}
                    />
                  </div>
                  <button
                    type="button"
                    className={`${styles.buttonSecondary} ${styles.prominentActionButton}`}
                    aria-label={`Xóa nguyên liệu ${index + 1}`}
                    onClick={() => removeIngredient(index)}
                  >
                    Xóa
                  </button>
                </div>
              ))}
            </div>
            {renderFieldError("ingredients")}
          </section>
          )}

          {/* Structured Steps Section */}
          {activeStep === "steps" && (
          <section aria-labelledby="cooking-steps-heading" style={{ display: "grid", gap: "12px" }}>
          <h2 id="cooking-steps-heading" style={{ fontSize: "16px" }}>Các bước chế biến</h2>
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
                  className={`${styles.buttonSecondary} ${styles.prominentActionButton}`}
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
                      className={styles.recipeEditorIconButton}
                      title="Di chuyển lên"
                      aria-label={`Di chuyển bước ${idx + 1} lên`}
                      disabled={idx === 0}
                      onClick={() => moveStep(idx, -1)}
                      style={{
                        cursor: idx === 0 ? "not-allowed" : "pointer",
                        opacity: idx === 0 ? 0.3 : 1,
                      }}
                    >
                      ↑
                    </button>
                    <button
                      type="button"
                      data-testid={`step-move-down-${idx}`}
                      className={styles.recipeEditorIconButton}
                      title="Di chuyển xuống"
                      aria-label={`Di chuyển bước ${idx + 1} xuống`}
                      disabled={idx === structuredSteps.length - 1}
                      onClick={() => moveStep(idx, 1)}
                      style={{
                        cursor: idx === structuredSteps.length - 1 ? "not-allowed" : "pointer",
                        opacity: idx === structuredSteps.length - 1 ? 0.3 : 1,
                      }}
                    >
                      ↓
                    </button>
                    <button
                      type="button"
                      data-testid={`step-delete-${idx}`}
                      className={`${styles.recipeEditorIconButton} ${styles.recipeEditorDeleteButton}`}
                      title="Xóa bước"
                      aria-label={`Xóa bước ${idx + 1}`}
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
                  aria-invalid={Boolean(fieldErrors.steps || errors.steps)}
                  aria-describedby={fieldErrors.steps || errors.steps ? "steps-error" : undefined}
                  placeholder="Mô tả chi tiết các thao tác thực hiện trong bước này..."
                  required
                  rows={2}
                  value={st.description}
                  onChange={(e) => updateStepField(idx, "description", e.target.value)}
                  style={{ padding: "6px 8px", fontSize: "12px" }}
                />

                <label className={styles.field} htmlFor={`step-image-file-${idx}`}>
                  Ảnh bước nấu {idx + 1}
                  <input
                    id={`step-image-file-${idx}`}
                    data-testid={`step-image-file-${idx}`}
                    type="file"
                    accept="image/*"
                    onChange={(event) => handleStepImageSelection(idx, event)}
                  />
                </label>
                {(st.imagePreviewUrl || st.imageUrl) && (
                  <img
                    className={styles.recipeImage}
                    src={st.imagePreviewUrl ?? st.imageUrl ?? undefined}
                    alt={`Ảnh bước nấu ${idx + 1}`}
                  />
                )}
              </div>
            ))}

            {structuredSteps.length === 0 && (
              <label className={styles.field} style={{ margin: 0 }}>
                Nhập nhanh các bước chế biến (mỗi bước một dòng)
                <textarea
                  id="steps"
                  aria-invalid={Boolean(fieldErrors.steps || errors.steps)}
                  aria-describedby={fieldErrors.steps || errors.steps ? "steps-error" : undefined}
                  {...register("steps", { onChange: () => clearFieldError("steps") })}
                  placeholder={"Sơ chế nguyên liệu.\nNấu và nêm nếm.\nTrình bày ra đĩa."}
                />
              </label>
            )}
          </div>
          </section>
          )}

          {activeStep === "general" && (
          <section aria-label="Thông tin chung" style={{ display: "grid", gap: "16px" }}>
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
                role="radiogroup"
                aria-label="Chọn ảnh chính của công thức"
                style={{ display: "flex", flexWrap: "wrap", gap: "8px", marginBottom: "12px" }}
              >
                {visibleImages.length === 0 && (
                  <span style={{ color: "var(--color-muted)", fontSize: "13px" }}>
                    Chưa có ảnh.
                  </span>
                )}
                {visibleImages.map((img, index) => (
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
                      <label style={{ display: "flex", alignItems: "center", gap: "3px", fontSize: "10px" }}>
                        <input
                          type="radio"
                          name="primaryRecipeImage"
                          aria-label={`Đặt ảnh ${index + 1} làm ảnh chính`}
                          checked={img.isPrimary}
                          onChange={(event) => {
                            if (event.target.checked) void handleSetPrimary(img.id);
                          }}
                          style={{
                            accentColor: "var(--forest)",
                            cursor: "pointer",
                          }}
                        />
                        Ảnh chính
                      </label>
                      <button
                        type="button"
                        data-testid={`btn-delete-image-${img.id}`}
                        aria-label={`Xóa ảnh ${index + 1}`}
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
              <p role="status" aria-live="polite" style={{ fontSize: "12px", color: "var(--color-muted)" }}>
                Ảnh là tùy chọn. Chọn một ảnh để đặt làm ảnh chính; có thể xóa tất cả ảnh.
                {hasPendingGalleryChanges && " Thay đổi ảnh đang chờ lưu; nhấn Cập nhật công thức để áp dụng."}
              </p>
              <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                <label className={styles.field} htmlFor="imageAltText">
                  Mô tả ảnh (Alt text) cho ảnh sắp tải lên (không bắt buộc)
                </label>
                <p id="imageAltTextHelp" style={{ margin: 0, fontSize: "12px", color: "var(--color-muted)" }}>
                  Mô tả nội dung ảnh cho người dùng trình đọc màn hình.
                </p>
                <input
                  id="imageAltText"
                  aria-describedby="imageAltTextHelp"
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
                <label className={styles.field} htmlFor="imageFile">
                  Tải thêm ảnh (JPG, PNG, WebP hoặc AVIF)
                </label>
                <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
                  <input
                    ref={fileInputRef}
                    id="imageFile"
                    type="file"
                    accept=".jpg,.jpeg,.png,.webp,.avif"
                    disabled={uploadingImage || visibleImages.length >= 10}
                    onChange={(e) => {
                      const f = e.target.files?.[0];
                      if (editing) {
                        handlePendingGalleryImageSelection(f);
                      } else {
                        handlePendingImageSelection(f);
                      }
                    }}
                    style={{ fontSize: "13px", flex: 1 }}
                  />
                  {uploadingImage && (
                    <span style={{ fontSize: "13px", color: "var(--color-muted)" }}>
                      Đang tải...
                    </span>
                  )}
                </div>
                {visibleImages.length >= 10 && (
                  <span style={{ fontSize: "12px", color: "var(--color-error)" }}>
                    Đã đạt giới hạn 10 ảnh.
                  </span>
                )}
              </div>
            </div>
          )}
          {!editing && (
            <section
              style={{
                display: "grid",
                gap: "10px",
                padding: "16px",
                border: "1px solid var(--line)",
                borderRadius: "6px",
                background: "var(--color-surface)",
              }}
            >
              <strong>Ảnh đại diện công thức (không bắt buộc)</strong>
              <label className={styles.field} htmlFor="imageAltText">
                Mô tả ảnh (Alt text) (không bắt buộc)
              </label>
              <p id="imageAltTextHelp" style={{ margin: 0, fontSize: "12px", color: "var(--color-muted)" }}>
                Mô tả nội dung ảnh cho người dùng trình đọc màn hình.
              </p>
              <input
                id="imageAltText"
                aria-describedby="imageAltTextHelp"
                value={imageAltText}
                onChange={(event) => setImageAltText(event.target.value)}
              />
              <label style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                <input
                  id="imageSetPrimary"
                  type="checkbox"
                  checked={imageSetPrimary}
                  onChange={(event) => setImageSetPrimary(event.target.checked)}
                />
                Đặt làm ảnh chính
              </label>
              <label className={styles.field} htmlFor="imageFile">
                Ảnh đại diện (JPG, PNG, WebP hoặc AVIF)
              </label>
              <input
                ref={fileInputRef}
                id="imageFile"
                type="file"
                accept=".jpg,.jpeg,.png,.webp,.avif"
                disabled={saving || uploadingImage || Boolean(createdRecipeId)}
                onChange={(event) => handlePendingImageSelection(event.target.files?.[0])}
              />
              {pendingImagePreviewUrl && (
                <img
                  className={styles.recipeImage}
                  src={pendingImagePreviewUrl}
                  alt={imageAltText || title || "Ảnh công thức"}
                />
              )}
              {uploadingImage && <span role="status">Đang tải ảnh lên...</span>}
              {createdRecipeId && hasPendingImageUploads && (
                <button
                  className={styles.buttonSecondary}
                  type="button"
                  onClick={() => void retryPendingImageUpload()}
                  disabled={saving || uploadingImage}
                >
                  Thử tải ảnh lại
                </button>
              )}
              <p style={{ fontSize: "12px", color: "var(--color-muted)", fontStyle: "italic" }}>
                Ảnh sẽ được tải lên sau khi lưu bản nháp.
              </p>
            </section>
          )}
          </section>
          )}

          {activeStep === "review" && (
            <section aria-labelledby="recipe-review-heading" style={{ display: "grid", gap: "12px" }}>
              <h2 id="recipe-review-heading" style={{ fontSize: "18px" }}>Xem lại công thức</h2>
              <dl style={{ display: "grid", gridTemplateColumns: "max-content 1fr", gap: "8px 12px" }}>
                <dt>Tiêu đề</dt><dd>{formValues.title}</dd>
                <dt>Mô tả</dt><dd>{formValues.description || "—"}</dd>
                <dt>Danh mục</dt>
                <dd>{categories.find((item) => item.id === formValues.categoryId)?.name ?? "—"}</dd>
                <dt>Độ khó</dt><dd>{formValues.difficulty}</dd>
                <dt>Thời gian chuẩn bị</dt><dd>{formValues.prepTime} phút</dd>
                <dt>Thời gian nấu</dt><dd>{formValues.cookTime} phút</dd>
                <dt>Khẩu phần</dt><dd>{formValues.servings}</dd>
              </dl>

              <section aria-labelledby="review-ingredients-heading">
                <h3 id="review-ingredients-heading">Nguyên liệu</h3>
                {formValues.ingredients.filter((ingredient) => ingredient.name.trim()).length > 0 ? (
                  <ul>
                    {formValues.ingredients
                      .filter((ingredient) => ingredient.name.trim())
                      .map((ingredient, index) => (
                        <li key={`${ingredient.name}-${index}`}>
                          {[ingredient.quantity, ingredient.unit, ingredient.name]
                            .filter(Boolean)
                            .join(" ")}
                          {ingredient.notes.trim() ? ` (${ingredient.notes.trim()})` : ""}
                        </li>
                      ))}
                  </ul>
                ) : <p>Chưa có nguyên liệu.</p>}
              </section>

              <section aria-labelledby="review-steps-heading">
                <h3 id="review-steps-heading">Các bước chế biến</h3>
                {reviewSteps.length > 0 ? (
                  <ol>
                    {reviewSteps.map((step, index) => (
                      <li key={`${step.id ?? "step"}-${index}`}>
                        {step.title.trim() && <strong>{step.title.trim()}: </strong>}
                        {step.description}
                      </li>
                    ))}
                  </ol>
                ) : <p>Chưa có bước chế biến.</p>}
              </section>

              <section aria-labelledby="review-nutrition-heading">
                <h3 id="review-nutrition-heading">Dinh dưỡng</h3>
                {includeNutrition ? (
                  <p>
                    {nutrition.calories} kcal · {nutrition.protein} g protein ·{" "}
                    {nutrition.carbohydrates} g carbs · {nutrition.fat} g chất béo ·{" "}
                    {nutrition.fiber} g chất xơ · {nutrition.sodium} mg natri
                  </p>
                ) : <p>Không bao gồm thông tin dinh dưỡng.</p>}
              </section>

              <section aria-labelledby="review-image-heading">
                <h3 id="review-image-heading">Ảnh công thức</h3>
                {reviewImageUrl ? (
                  /* eslint-disable-next-line @next/next/no-img-element */
                  <img
                    className={styles.recipeImage}
                    src={reviewImageUrl}
                    alt={imageAltText || images.find((image) => image.isPrimary)?.altText || formValues.title}
                  />
                ) : <p>Chưa chọn ảnh.</p>}
              </section>
              {createdRecipeId && hasPendingImageUploads && (
                <button
                  className={styles.buttonSecondary}
                  type="button"
                  onClick={() => void retryPendingImageUpload()}
                  disabled={saving || uploadingImage}
                >
                  Thử tải ảnh lại
                </button>
              )}
            </section>
          )}

          <div className={`${styles.actions} ${styles.recipeWizardActions}`}>
            {activeStep !== "general" && (
              <button
                className={styles.buttonSecondary}
                type="button"
                onClick={goToPreviousStep}
              >
                Quay lại
              </button>
            )}
            {(activeStep === "general" || activeStep === "ingredients") && (
              <button
                className={styles.button}
                type="button"
                onClick={() => void advanceWizard()}
              >
                Tiếp theo
              </button>
            )}
            {activeStep === "steps" && (
              <button
                className={styles.button}
                type="button"
                onClick={() => void advanceWizard()}
              >
                Xem lại
              </button>
            )}
            {activeStep === "review" && (
              <button className={styles.button} type="submit" disabled={saving || !categoryId}>
                {saving ? "Đang lưu..." : editing ? "Cập nhật công thức" : "Tạo bản nháp"}
              </button>
            )}
            <Link className={styles.buttonSecondary} href="/dashboard/recipes">
              Hủy
            </Link>
          </div>
        </form>
      )}
    </main>
  );
}
