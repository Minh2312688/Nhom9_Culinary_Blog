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
            {steps.map((step, index) => <li key={step.id ?? index}>
              <strong>{step.title || `Bước ${step.stepNumber ?? index + 1}`}</strong>
              <div>{step.description}</div>
            </li>)}
          </ol> : <p className={styles.notice}>Chưa có hướng dẫn chế biến.</p>}
        </section>
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
