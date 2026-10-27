import type { RecipeDetail } from "./recipes-api";

export interface RecipeJsonLd {
  "@context": "https://schema.org";
  "@type": "Recipe";
  name: string;
  description?: string;
  image?: string[];
  datePublished?: string;
  author?: {
    "@type": "Person";
    name: string;
  };
  prepTime: string;
  cookTime: string;
  recipeYield: string;
  recipeIngredient: string[];
  recipeInstructions: Array<{
    "@type": "HowToStep";
    name?: string;
    text: string;
    image?: string;
    url?: string;
  }>;
  nutrition?: {
    "@type": "NutritionInformation";
    calories: string;
    proteinContent: string;
    carbohydrateContent: string;
    fatContent: string;
    fiberContent: string;
    sodiumContent: string;
  };
}

function isoDuration(minutes: number): string {
  return `PT${minutes}M`;
}

export function buildRecipeJsonLd(recipe: RecipeDetail): RecipeJsonLd {
  const jsonLd: RecipeJsonLd = {
    "@context": "https://schema.org",
    "@type": "Recipe",
    name: recipe.title,
    prepTime: isoDuration(recipe.prepTimeMinutes),
    cookTime: isoDuration(recipe.cookTimeMinutes),
    recipeYield: `${recipe.servings} servings`,
    recipeIngredient: recipe.ingredients
      .slice()
      .sort((first, second) => first.orderIndex - second.orderIndex)
      .map((ingredient) => [
        ingredient.quantity,
        ingredient.unit,
        ingredient.name,
      ].filter((part) => part !== null && part !== undefined && part !== "").join(" ")),
    recipeInstructions: recipe.steps
      .slice()
      .sort((first, second) => (first.stepNumber ?? 0) - (second.stepNumber ?? 0))
      .map((step) => ({
        "@type": "HowToStep" as const,
        ...(step.title ? { name: step.title } : {}),
        text: step.description,
        ...(step.imageUrl ? { image: step.imageUrl } : {}),
      })),
  };

  if (recipe.description) jsonLd.description = recipe.description;
  if (recipe.publishedAt) jsonLd.datePublished = recipe.publishedAt;
  if (recipe.author?.displayName.trim()) {
    jsonLd.author = { "@type": "Person", name: recipe.author.displayName };
  }

  const sortedImages = recipe.images
    .slice()
    .sort((first, second) => Number(second.isPrimary) - Number(first.isPrimary) ||
      first.orderIndex - second.orderIndex)
    .map((image) => image.mediumUrl ?? image.thumbnailUrl ?? image.originalUrl)
    .filter(Boolean);
  if (sortedImages.length > 0) jsonLd.image = sortedImages;

  if (recipe.nutrition) {
    jsonLd.nutrition = {
      "@type": "NutritionInformation",
      calories: `${recipe.nutrition.calories} calories`,
      proteinContent: `${recipe.nutrition.protein} g`,
      carbohydrateContent: `${recipe.nutrition.carbohydrates} g`,
      fatContent: `${recipe.nutrition.fat} g`,
      fiberContent: `${recipe.nutrition.fiber} g`,
      sodiumContent: `${recipe.nutrition.sodium} mg`,
    };
  }

  return jsonLd;
}

export function serializeRecipeJsonLd(recipe: RecipeJsonLd): string {
  return JSON.stringify(recipe).replace(/</g, "\\u003c");
}
