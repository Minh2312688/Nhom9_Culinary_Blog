import assert from "node:assert/strict";
import test from "node:test";
import type { RecipeDetail } from "./recipes-api.ts";
import { buildRecipeJsonLd, serializeRecipeJsonLd } from "./recipe-json-ld.ts";

function recipe(overrides: Partial<RecipeDetail> = {}): RecipeDetail {
  return {
    id: "recipe-1",
    title: "Roasted tomato soup",
    slug: "roasted-tomato-soup",
    description: "A simple soup.",
    difficulty: "Easy",
    cookTimeMinutes: 30,
    prepTimeMinutes: 10,
    servings: 4,
    status: "Published",
    categoryId: "category-1",
    authorId: "author-1",
    rowVersion: "v1",
    publishedAt: "2026-10-01T09:30:00Z",
    author: { id: "author-1", displayName: "Linh Nguyen", avatarUrl: null },
    ingredients: [
      { name: "Tomatoes", quantity: 4, unit: null, notes: null, orderIndex: 0 },
    ],
    steps: [
      { stepNumber: 1, title: null, description: "Roast the tomatoes.", durationMinutes: null, imageUrl: null },
    ],
    images: [],
    nutrition: {
      calories: 120,
      protein: 3,
      carbohydrates: 18,
      fat: 4,
      fiber: 5,
      sodium: 300,
    },
    ...overrides,
  };
}

test("builds Recipe JSON-LD from the published author and publication date", () => {
  assert.deepEqual(buildRecipeJsonLd(recipe()), {
    "@context": "https://schema.org",
    "@type": "Recipe",
    name: "Roasted tomato soup",
    description: "A simple soup.",
    datePublished: "2026-10-01T09:30:00Z",
    author: { "@type": "Person", name: "Linh Nguyen" },
    prepTime: "PT10M",
    cookTime: "PT30M",
    recipeYield: "4 servings",
    recipeIngredient: ["4 Tomatoes"],
    recipeInstructions: [
      { "@type": "HowToStep", text: "Roast the tomatoes." },
    ],
    nutrition: {
      "@type": "NutritionInformation",
      calories: "120 calories",
      proteinContent: "3 g",
      carbohydrateContent: "18 g",
      fatContent: "4 g",
      fiberContent: "5 g",
      sodiumContent: "300 mg",
    },
  });
});

test("omits datePublished when a historical recipe has no publication date", () => {
  const jsonLd = buildRecipeJsonLd(recipe({ publishedAt: null }));

  assert.equal("datePublished" in jsonLd, false);
});

test("omits author when public author data is absent", () => {
  const jsonLd = buildRecipeJsonLd(recipe({ author: null }));

  assert.equal("author" in jsonLd, false);
});

test("omits nutrition when the recipe has no nutrition data", () => {
  const jsonLd = buildRecipeJsonLd(recipe({ nutrition: null }));

  assert.equal("nutrition" in jsonLd, false);
});

test("escapes less-than characters when serializing hostile recipe content", () => {
  const json = serializeRecipeJsonLd(buildRecipeJsonLd(recipe({
    title: "</script><script>alert('xss')</script>",
  })));

  assert.equal(json.includes("<"), false);
  assert.equal(json.includes("\\u003c/script>"), true);
});
