"use client";

import Link from "next/link";
import type { RecipeDetail } from "@/lib/recipes-api";
import { ErrorNotice, SitePage } from "../../site-ui";
import styles from "../../site-ui.module.css";

export function RecipeDetailClient({ recipe }: Readonly<{ recipe: RecipeDetail }>) {
  const images = recipe.images
    .slice()
    .sort((first, second) => Number(second.isPrimary) - Number(first.isPrimary) ||
      first.orderIndex - second.orderIndex);
  const ingredients = recipe.ingredients
    .slice()
    .sort((first, second) => first.orderIndex - second.orderIndex);
  const steps = recipe.steps
    .slice()
    .sort((first, second) => (first.stepNumber ?? 0) - (second.stepNumber ?? 0));

  return (
    <SitePage>
      <main className={styles.detail}>
        <p className={styles.eyebrow}><Link href="/recipes">CÔNG THỨC</Link> / CHI TIẾT</p>
        <h1>{recipe.title}</h1>
        {recipe.publishedAt && (
          <p className={styles.cardMeta}>
            <time dateTime={recipe.publishedAt}>
              {new Date(recipe.publishedAt).toLocaleDateString("vi-VN", {
                day: "numeric",
                month: "long",
                year: "numeric",
                timeZone: "UTC",
              })}
            </time>
          </p>
        )}
        {recipe.author && (
          <p className={styles.cardMeta}>
            Tác giả{" "}
            <Link href={`/authors/${encodeURIComponent(recipe.author.id)}`}>
              {recipe.author.displayName}
            </Link>
          </p>
        )}
        {recipe.description && <p className={styles.detailLead}>{recipe.description}</p>}
        {images.length > 0 && (
          <div className={styles.recipeImages}>
            {images.map((image) => (
              <img
                className={styles.recipeImage}
                key={image.id}
                src={image.mediumUrl ?? image.thumbnailUrl ?? image.originalUrl}
                alt={image.altText ?? recipe.title}
                loading="lazy"
              />
            ))}
          </div>
        )}
        <div className={styles.cardMeta}>
          <span>Chuẩn bị {recipe.prepTimeMinutes} phút</span>
          <span>Nấu {recipe.cookTimeMinutes} phút</span>
          <span>{recipe.servings} khẩu phần</span>
          <span>{recipe.difficulty}</span>
        </div>
        <section className={styles.detailSection}>
          <h2>Nguyên liệu</h2>
          {ingredients.length ? <ul className={styles.ingredientList}>
            {ingredients.map((ingredient, index) => (
              <li key={ingredient.id ?? `${ingredient.name}-${index}`}>
                {[ingredient.quantity, ingredient.unit, ingredient.name].filter(Boolean).join(" ")}
                {ingredient.notes && ` (${ingredient.notes})`}
              </li>
            ))}
          </ul> : <p className={styles.notice}>Chưa có nguyên liệu.</p>}
        </section>
        <section className={styles.detailSection}>
          <h2>Cách thực hiện</h2>
          {steps.length ? <ol className={styles.stepList}>
            {steps.map((step, index) => (
              <li key={step.id ?? index} data-testid={`recipe-step-${step.stepNumber ?? index + 1}`}>
                <div style={{ display: "flex", alignItems: "baseline", gap: "8px", flexWrap: "wrap" }}>
                  <strong>{step.title ? `Bước ${step.stepNumber ?? index + 1}: ${step.title}` : `Bước ${step.stepNumber ?? index + 1}`}</strong>
                  {step.durationMinutes && (
                    <span style={{ fontSize: "12px", color: "var(--color-muted)", background: "var(--color-surface)", padding: "1px 6px", borderRadius: "4px", border: "1px solid var(--line)" }}>
                      ⏱ {step.durationMinutes} phút
                    </span>
                  )}
                </div>
                <div style={{ marginTop: "4px" }}>{step.description}</div>
                {step.imageUrl && (
                  <div style={{ marginTop: "8px" }}>
                    <img
                      src={step.imageUrl}
                      alt={step.title ?? `Hình minh họa bước ${step.stepNumber ?? index + 1}`}
                      style={{ maxWidth: "320px", maxHeight: "200px", borderRadius: "6px", objectFit: "cover", border: "1px solid var(--line)" }}
                      loading="lazy"
                    />
                  </div>
                )}
              </li>
            ))}
          </ol> : <p className={styles.notice}>Chưa có hướng dẫn chế biến.</p>}
        </section>
        {recipe.nutrition && (
          <section className={styles.detailSection} data-testid="recipe-nutrition">
            <h2>Thông tin dinh dưỡng (mỗi khẩu phần)</h2>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(110px, 1fr))", gap: "10px", marginTop: "8px" }}>
              <div style={{ display: "flex", flexDirection: "column", gap: "2px", padding: "8px 12px", background: "var(--color-surface)", borderRadius: "6px", border: "1px solid var(--line)" }}>
                <span style={{ fontSize: "11px", color: "var(--color-muted)" }}>Calories</span>
                <strong style={{ fontSize: "15px" }}>{recipe.nutrition.calories} kcal</strong>
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: "2px", padding: "8px 12px", background: "var(--color-surface)", borderRadius: "6px", border: "1px solid var(--line)" }}>
                <span style={{ fontSize: "11px", color: "var(--color-muted)" }}>Protein</span>
                <strong style={{ fontSize: "15px" }}>{recipe.nutrition.protein} g</strong>
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: "2px", padding: "8px 12px", background: "var(--color-surface)", borderRadius: "6px", border: "1px solid var(--line)" }}>
                <span style={{ fontSize: "11px", color: "var(--color-muted)" }}>Carbs</span>
                <strong style={{ fontSize: "15px" }}>{recipe.nutrition.carbohydrates} g</strong>
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: "2px", padding: "8px 12px", background: "var(--color-surface)", borderRadius: "6px", border: "1px solid var(--line)" }}>
                <span style={{ fontSize: "11px", color: "var(--color-muted)" }}>Chất béo</span>
                <strong style={{ fontSize: "15px" }}>{recipe.nutrition.fat} g</strong>
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: "2px", padding: "8px 12px", background: "var(--color-surface)", borderRadius: "6px", border: "1px solid var(--line)" }}>
                <span style={{ fontSize: "11px", color: "var(--color-muted)" }}>Chất xơ</span>
                <strong style={{ fontSize: "15px" }}>{recipe.nutrition.fiber} g</strong>
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: "2px", padding: "8px 12px", background: "var(--color-surface)", borderRadius: "6px", border: "1px solid var(--line)" }}>
                <span style={{ fontSize: "11px", color: "var(--color-muted)" }}>Natri</span>
                <strong style={{ fontSize: "15px" }}>{recipe.nutrition.sodium} mg</strong>
              </div>
            </div>
          </section>
        )}
      </main>
    </SitePage>
  );
}

export function RecipeDetailError({ message }: Readonly<{ message: string }>) {
  return (
    <SitePage>
      <main className={styles.detail}>
        <ErrorNotice>{message}</ErrorNotice>
      </main>
    </SitePage>
  );
}
