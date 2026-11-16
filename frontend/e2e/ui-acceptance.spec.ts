import { expect, test } from "@playwright/test";
import { signInAs, signInAsByApi } from "./support/auth";
import {
  clearMockApiRequests,
  queueMockApiResponse,
} from "./support/mock-api";

const mockApiUrl = "http://127.0.0.1:5055";
const category = {
  id: "00000000-0000-0000-0000-000000000002",
  name: "Món Việt",
  slug: "mon-viet",
  description: "Công thức Việt",
  imageUrl: null,
  orderIndex: 0,
  recipeCount: 1,
};
const emptyRecipes = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 12,
  totalPages: 0,
  hasNextPage: false,
  hasPreviousPage: false,
};

test.beforeEach(async ({ page }) => {
  const reset = await page.context().request.post(`${mockApiUrl}/__test/auth/reset`);
  expect(reset.status()).toBe(204);
  await signInAs(page);
  await clearMockApiRequests(page);
  await page.route("**/api/v1/categories**", (route) => route.fulfill({
    json: { items: [category], totalCount: 1, page: 1, pageSize: 12, totalPages: 1, hasNextPage: false, hasPreviousPage: false },
  }));
});

test("renders API validation errors beside Recipe fields and in an error summary", async ({ page }) => {
  await queueMockApiResponse(page, {
    method: "POST",
    path: "/api/v1/recipes",
    status: 400,
    body: {
      title: "Validation failed",
      detail: "Vui lòng kiểm tra dữ liệu công thức.",
      errors: {
        Title: ["Tiêu đề đã được sử dụng."],
        UnknownField: ["Không thể lưu trường này."],
      },
    },
  });

  await page.goto("/dashboard/recipes/new");
  await page.getByLabel("Tiêu đề").fill("Bún bò Huế");
  await page.getByLabel("Danh mục").selectOption(category.id);
  await page.getByRole("button", { name: "Tiếp theo" }).click();
  await page.getByRole("button", { name: "Tiếp theo" }).click();
  await page.getByRole("button", { name: "Xem lại" }).click();
  await page.getByRole("button", { name: "Tạo bản nháp" }).click();

  const titleField = page.locator("#title");
  const titleError = page.getByText("Tiêu đề đã được sử dụng.");
  await expect(titleError).toHaveAttribute("role", "alert");
  await expect(titleField).toHaveAttribute("aria-describedby", /title-error/);
  await expect(page.locator("[data-error-summary]")).toContainText("Không thể lưu trường này.");
  await expect(page.locator("[data-error-summary]")).toContainText("Vui lòng kiểm tra dữ liệu công thức.");
});

test("maps Category validation errors to their associated input", async ({ page }) => {
  await signInAsByApi(page, "admin@example.com");
  await queueMockApiResponse(page, {
    method: "POST",
    path: "/api/v1/categories",
    status: 400,
    body: {
      title: "Validation failed",
      errors: { Name: ["Tên danh mục đã tồn tại."] },
    },
  });

  await page.goto("/dashboard/categories");
  await page.getByLabel("Tên danh mục").fill("Món chay");
  await page.getByRole("button", { name: "Tạo danh mục" }).click();

  await expect(page.getByLabel("Tên danh mục")).toHaveAttribute("aria-describedby", "name-error");
  await expect(page.getByText("Tên danh mục đã tồn tại.")).toHaveAttribute("role", "alert");
});

test("keeps a recipe draft unchanged when publishing fails", async ({ page }) => {
  const draft = {
    id: "00000000-0000-0000-0000-000000000001",
    title: "Bún bò Huế",
    slug: "bun-bo-hue",
    description: null,
    difficulty: "Easy",
    cookTimeMinutes: 30,
    servings: 2,
    status: "Draft",
    categoryId: category.id,
    authorId: "author-1",
    publishedAt: null,
    rowVersion: "AAAAAA==",
  };
  await queueMockApiResponse(page, {
    method: "GET",
    path: "/api/v1/recipes/mine",
    status: 200,
    body: { ...emptyRecipes, items: [draft], totalCount: 1 },
  });
  await queueMockApiResponse(page, {
    method: "PATCH",
    path: `/api/v1/recipes/${draft.id}/publish`,
    status: 409,
    body: { title: "Conflict", detail: "Không thể xuất bản bản nháp này." },
  });

  await page.goto("/dashboard/recipes");
  await expect(page.getByText("Bản nháp", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Xuất bản" }).click();

  await expect(page.getByText("Bản nháp", { exact: true })).toBeVisible();
  await expect(page.locator("[data-error-summary]")).toContainText("Không thể xuất bản bản nháp này.");
});

test("shows accessible loading feedback while dashboard recipes are loading", async ({ page }) => {
  await queueMockApiResponse(page, {
    method: "GET",
    path: "/api/v1/recipes/mine",
    status: 200,
    body: emptyRecipes,
    delayMs: 750,
  });

  await page.goto("/dashboard/recipes");
  await expect(page.getByRole("status")).toHaveAttribute("aria-busy", "true");
  await expect(page.getByText("Bạn chưa có công thức nào.")).toBeVisible();
});

test("shows one create-recipe action when the recipe list is empty", async ({ page }) => {
  await queueMockApiResponse(page, {
    method: "GET",
    path: "/api/v1/recipes/mine",
    status: 200,
    body: emptyRecipes,
  });

  await page.goto("/dashboard/recipes");

  const emptyState = page.getByRole("region", { name: "Chưa có công thức" });
  await expect(emptyState.getByText("Bạn chưa có công thức nào.")).toBeVisible();
  const createRecipe = page.getByRole("link", { name: "Tạo công thức" });
  await expect(createRecipe).toHaveCount(1);
  await expect(createRecipe).toHaveAttribute("href", "/dashboard/recipes/new");
  await expect(createRecipe).toHaveCSS("background-color", "rgb(183, 62, 45)");
  await expect(createRecipe).toHaveCSS("color", "rgb(255, 253, 248)");
});

test("announces successful Category creation", async ({ page }) => {
  await signInAsByApi(page, "admin@example.com");
  await queueMockApiResponse(page, {
    method: "POST",
    path: "/api/v1/categories",
    status: 201,
    body: { ...category, name: "Món chay" },
  });

  await page.goto("/dashboard/categories");
  await page.getByLabel("Tên danh mục").fill("Món chay");
  await page.getByRole("button", { name: "Tạo danh mục" }).click();

  await expect(page.getByRole("status")).toContainText("Đã tạo danh mục.");
});
