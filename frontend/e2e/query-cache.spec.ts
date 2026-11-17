import { expect, test } from "@playwright/test";
import {
  clearMockApiRequests,
  getMockApiRequests,
  queueMockApiResponse,
} from "./support/mock-api";
import { signInAsByApi } from "./support/auth";

test("retries once after a transient recipe API server error", async ({ page }) => {
  await clearMockApiRequests(page);
  await queueMockApiResponse(page, {
    method: "GET",
    path: "/api/v1/recipes/",
    status: 503,
  });

  await page.goto("/recipes");
  await expect(page.getByRole("link", { name: /Roasted tomato soup/ })).toBeVisible();

  const recipeListRequests = (await getMockApiRequests(page)).filter((request) =>
    request.method === "GET" && request.path === "/api/v1/recipes/",
  );

  expect(recipeListRequests).toHaveLength(2);
});

test("reuses a fresh search result when returning to the same filters", async ({ page }) => {
  const searchRequests: string[] = [];
  await page.route("**/api/v1/recipes/search?**", async (route) => {
    searchRequests.push(route.request().url());
    await route.fulfill({
      json: {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 12,
        totalPages: 0,
        hasNextPage: false,
        hasPreviousPage: false,
      },
    });
  });

  await page.goto("/search?q=pho");
  await expect.poll(() => searchRequests.length).toBe(1);

  const searchInput = page.locator("#recipe-search");
  await searchInput.fill("bun");
  await expect.poll(() => searchRequests.length).toBe(2);
  await searchInput.fill("pho");
  await page.waitForTimeout(400);

  expect(searchRequests.map((url) => new URL(url).searchParams.get("q"))).toEqual(["pho", "bun"]);
});

test("reuses the Dashboard recipe list across client navigation", async ({ page }) => {
  await signInAsByApi(page);
  await clearMockApiRequests(page);
  await page.goto("/dashboard/recipes");
  await expect(page.getByRole("row", { name: /Roasted tomato soup/ })).toBeVisible();

  await page.getByRole("link", { name: "Về trang chủ" }).click();
  await page.getByTestId("nav-my-recipes").click();
  await expect(page.getByRole("row", { name: /Roasted tomato soup/ })).toBeVisible();

  const listRequests = (await getMockApiRequests(page)).filter((request) =>
    request.method === "GET" && request.path === "/api/v1/recipes/mine",
  );
  expect(listRequests).toHaveLength(1);
});

test("reuses the Dashboard category list across client navigation", async ({ page }) => {
  await signInAsByApi(page, "admin@example.com");
  await clearMockApiRequests(page);
  await page.goto("/dashboard/categories");
  await expect(page.getByRole("row", { name: /Món chính/ })).toBeVisible();

  await page.getByRole("link", { name: "Về trang chủ" }).click();
  await clearMockApiRequests(page);
  await page.getByTestId("nav-my-recipes").click();
  await expect(page).toHaveURL(/\/dashboard\/recipes$/);
  await page.getByRole("navigation", { name: "Điều hướng bảng điều khiển" })
    .getByRole("link", { name: "Danh mục" }).click();
  await expect(page.getByRole("row", { name: /Món chính/ })).toBeVisible();

  const categoryRequests = (await getMockApiRequests(page)).filter((request) =>
    request.method === "GET" && request.path === "/api/v1/categories/",
  );
  expect(categoryRequests).toHaveLength(0);
});
