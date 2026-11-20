import type { RecipeStep } from "./recipes-api";

export function getRecipeStepImageUrls(
  step: RecipeStep & { imagePreviewUrl?: string | null },
  index: number,
  recipeImages: ReadonlyArray<{ originalUrl: string; stepNumber?: number | null }> = [],
): string[] {
  const stepNumber = step.stepNumber ?? index + 1;
  const urls: string[] = [];

  for (const image of recipeImages) {
    if (image.stepNumber != null) {
      if (image.stepNumber === stepNumber) {
        urls.push(image.originalUrl);
      }
      continue;
    }

    const url = image.originalUrl;
    const fileName = url.split(/[?#]/, 1)[0].split("/").pop()?.toLowerCase();
    if (!fileName) continue;

    const match = /^step(\d+)(?:-\d+)?\.(?:avif|jpe?g|png|webp)$/i.exec(fileName);
    if (match && Number(match[1]) === stepNumber) {
      urls.push(image.originalUrl);
    }
  }

  if (step.imageUrl && !urls.includes(step.imageUrl)) {
    urls.unshift(step.imageUrl);
  }
  if (step.imagePreviewUrl && !urls.includes(step.imagePreviewUrl)) {
    urls.unshift(step.imagePreviewUrl);
  }

  return [...new Set(urls)];
}
