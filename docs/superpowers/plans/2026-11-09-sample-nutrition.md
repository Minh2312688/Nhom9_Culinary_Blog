# Sample Recipe Nutrition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add estimated per-serving nutrition to all 20 sample recipes and populate existing seeded rows without overwriting user-maintained nutrition.

**Architecture:** Extend the sample JSON and loader model with the six nutrition fields already supported by the domain. New recipes receive seed nutrition at creation; existing sample recipes receive it only when their nutrition relation is missing. Label displayed values as reference estimates.

**Tech Stack:** .NET 10, System.Text.Json, EF Core, PostgreSQL, Next.js 15, TypeScript, Playwright.

**Spec:** [2026-11-09-recipe-category-media-and-nutrition-design.md](../specs/2026-11-09-recipe-category-media-and-nutrition-design.md)

## Global Constraints

- Add all six existing nutrition fields to each of the 20 sample recipe entries: calories, protein, carbohydrates, fat, fiber, and sodium.
- Values are estimates per serving derived from the recipe ingredients and serving count, not verified laboratory values.
- During seed, fill missing nutrition on existing sample recipes and leave existing nutrition untouched. Newly seeded recipes receive the same data.
- No schema migration is required.
- Label sample nutrition values as estimates/reference values per serving.
- Do not remove PostgreSQL, Redis, MinIO, or Seq volumes during container rebuild/recreation.

---

### Task 1: Validate and persist sample nutrition idempotently

**Files:**
- Modify: `tools/sample-data/20_cong_thuc_mon_an.json`
- Modify: `backend/src/CulinaryBlog.Infrastructure/Persistence/Seed/SampleRecipeDataLoader.cs`
- Modify: `backend/src/CulinaryBlog.Infrastructure/Persistence/Seed/Lab02DataSeeder.cs`
- Test: `backend/tests/CulinaryBlog.Application.Tests/Infrastructure/Persistence/SampleRecipeDataLoaderTests.cs`
- Test: `backend/tests/CulinaryBlog.Integration.Tests/Persistence/SampleRecipeSeederTests.cs`
- Update sample JSON fixtures in tests that call `SampleRecipeDataLoader.Load`.

**Interfaces:**
- Add a `Nutrition` object to `SampleRecipeData`, shaped exactly like `RecipeNutritionInput` and `RecipeNutritionDto`: `Calories`, `Protein`, `Carbohydrates`, `Fat`, `Fiber`, `Sodium`.
- Existing domain entity `RecipeNutrition` and API contracts remain unchanged.

- [ ] **Step 1: Add loader and seed regression tests**

Add a loader test asserting all six values deserialize and missing/negative nutrition is rejected for seed recipes. Extend the seeder test to seed an existing recipe with null nutrition and an existing recipe with populated nutrition; assert the former is filled and the latter remains unchanged.

```csharp
Assert.Equal(310, sample.Nutrition.Calories);
Assert.NotNull(seededRecipe.Nutrition);
Assert.Equal(310, seededRecipe.Nutrition!.Calories);
Assert.Equal(999, existingRecipe.Nutrition!.Calories);
```

- [ ] **Step 2: Run focused tests and verify they fail**

Run:
- `dotnet test backend\tests\CulinaryBlog.Application.Tests\CulinaryBlog.Application.Tests.csproj --filter FullyQualifiedName~SampleRecipeDataLoaderTests`
- `dotnet test backend\tests\CulinaryBlog.Integration.Tests\CulinaryBlog.Integration.Tests.csproj --filter FullyQualifiedName~SampleRecipeSeederTests`

Expected: loader values are missing and seeding does not populate existing recipes.

- [ ] **Step 3: Add six estimates per recipe in the JSON**

For each of the 20 records, add a `nutrition` object with the six numeric properties. Estimate per serving from ingredients and serving count; use values within each type's valid range and do not present them as verified clinical data. Keep JSON formatting and recipe order unchanged.

```json
"nutrition": {
  "calories": 310,
  "protein": 18.5,
  "carbohydrates": 36.0,
  "fat": 10.2,
  "fiber": 4.1,
  "sodium": 680
}
```

- [ ] **Step 4: Validate all six fields and extend existing seed behavior**

Add a `SampleRecipeNutrition` model to the sample data model; validate required nutrition and non-negative values. Map it to `RecipeNutrition` when creating a recipe. Include `Nutrition` when loading existing sample recipes and, only if null, add a nutrition entity from the JSON values. Save through the existing context and preserve any non-null relation.

- [ ] **Step 5: Verify 20 records and seed tests**

Run the focused tests again. Also parse the JSON and assert every one of its 20 records contains exactly the six numeric fields. Expected: all tests pass, and the integration test skips only when Docker is unavailable.

### Task 2: Mark nutrition as an estimate in recipe detail

**Files:**
- Modify: `frontend/src/app/recipes/[slug]/recipe-detail-client.tsx`
- Test: `frontend/e2e/recipe-crud-flow.spec.ts`
- Modify: `frontend/e2e/support/mock-recipe-api.mjs`

**Interfaces:**
- No API or TypeScript model changes; existing detail nutrition shape is complete.

- [ ] **Step 1: Add a label assertion**

In the nutrition detail E2E test, assert the nutrition heading or note includes both per-serving and reference/estimate wording.

```ts
await expect(nutritionSec).toContainText(/ước tính|tham khảo/i);
await expect(nutritionSec).toContainText(/mỗi khẩu phần/i);
```

- [ ] **Step 2: Run the focused browser test and verify failure**

Run from `frontend`: `.\node_modules\.bin\playwright.cmd test e2e/recipe-crud-flow.spec.ts --grep "Recipe Detail"`

Expected: the current detail page has no estimate/reference label.

- [ ] **Step 3: Add clear Vietnamese reference copy**

Add a short note inside the nutrition section such as `Giá trị ước tính để tham khảo, tính trên mỗi khẩu phần.` Keep the six existing nutrient labels and values.

- [ ] **Step 4: Run nutrition, browser, and build checks**

Run:
- `.\node_modules\.bin\playwright.cmd test e2e/recipe-crud-flow.spec.ts --grep "Recipe Detail"`
- `npm run build`

Expected: the estimate note and six values render without changing recipe detail metadata.

## Integration and documentation

- Update `README.md` and `docs/docker-guide.md` to say the sample seed includes estimated per-serving nutrition and that rerunning seed only fills missing data.
- After all feature plans are complete and Docker is running, follow [the Docker rollout plan](2026-11-09-docker-rollout.md).
