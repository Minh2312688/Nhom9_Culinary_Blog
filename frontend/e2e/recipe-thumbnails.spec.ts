import { expect, test } from "@playwright/test";

const primaryImageUrl = "https://storage.example.test/main.webp";
const recipe = {
  id: "recipe-thumbnail",
  title: "Phở bò",
  slug: "pho-bo",
  description: "Nước dùng đậm đà",
  difficulty: "Easy",
  cookTimeMinutes: 45,
  servings: 4,
  status: "Published",
  categoryId: "category-thumbnail",
  authorId: "author-1",
  publishedAt: "2026-10-01T09:30:00Z",
  rowVersion: "v1",
  primaryImageUrl,
  primaryImageAltText: "Tô phở bò hoàn chỉnh",
};

const paginatedRecipes = {
  items: [recipe],
  totalCount: 1,
  page: 1,
  pageSize: 12,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

test("shows each recipe's primary image in home, recipe, search, and category lists", async ({ page }) => {
  await page.route("**/api/v1/recipes/**", (route) => route.fulfill({ json: paginatedRecipes }));
  await page.route("**/api/v1/categories/category-thumbnail", (route) => route.fulfill({
    json: { id: "category-thumbnail", name: "Món chính", slug: "mon-chinh" },
  }));
  await page.route("**/api/v1/categories**", (route) => route.fulfill({
    json: { items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0, hasNextPage: false, hasPreviousPage: false },
  }));

  for (const path of ["/", "/recipes", "/search?q=pho", "/categories/mon-chinh"]) {
    await page.goto(path);
    await expect(page.locator(`img[src="${primaryImageUrl}"]`), path).toBeVisible();
    await expect(page.locator(`img[alt="${recipe.primaryImageAltText}"]`), path).toHaveCount(1);
  }
});

test("shows the primary image in an author's recipe list", async ({ page }) => {
  await page.goto("/authors/author-1");

  await expect(page.locator('img[src="https://storage.example.test/main.webp"]')).toBeVisible();
  await expect(page.locator('img[alt="Roasted tomato soup"]')).toHaveCount(1);
});
