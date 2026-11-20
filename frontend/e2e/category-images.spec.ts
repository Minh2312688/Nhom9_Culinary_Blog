import { expect, test } from "@playwright/test";
import { signInAsByApi } from "./support/auth";
import {
  clearMockApiRequests,
  getMockApiRequests,
  queueMockApiResponse,
} from "./support/mock-api";

const category = {
  id: "00000000-0000-0000-0000-000000000010",
  name: "Món canh",
  slug: "mon-canh",
  description: "Canh thanh mát",
  imageUrl: null,
  orderIndex: 1,
  recipeCount: 2,
};

const categoriesResponse = {
  items: [category],
  totalCount: 1,
  page: 1,
  pageSize: 100,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

test.beforeEach(async ({ page }) => {
  const reset = await page.context().request.post("http://127.0.0.1:5055/__test/auth/reset");
  expect(reset.status()).toBe(204);
  await clearMockApiRequests(page);
});

test("Author cannot access Category management", async ({ page }) => {
  await signInAsByApi(page);
  await page.goto("/dashboard");
  await expect(page.getByRole("link", { name: "Danh mục" })).toHaveCount(0);
  await expect(page.locator('a[href="/dashboard/categories"]')).toHaveCount(0);
  await page.goto("/dashboard/categories");
  await expect(page).toHaveURL(/\/dashboard$/);
});

test("Admin can access Category management", async ({ page }) => {
  await signInAsByApi(page, "admin@example.com");
  await page.goto("/dashboard/categories");
  await expect(page.getByRole("heading", { level: 1, name: "Quản lý danh mục" })).toBeVisible();
  const dashboardNavigation = page.getByRole("navigation", { name: "Điều hướng bảng điều khiển" });
  await expect(dashboardNavigation.getByRole("link", { name: "Danh mục" })).toBeVisible();
  const navigationGap = await dashboardNavigation.evaluate((element) =>
    Number.parseFloat(window.getComputedStyle(element).columnGap),
  );
  expect(navigationGap).toBeGreaterThanOrEqual(16);
  await expect(page.getByRole("button", { name: "Sửa" }).first()).toBeVisible();

  const createButton = await page.getByRole("button", { name: "Tạo danh mục" }).boundingBox();
  const editButton = await page.getByRole("button", { name: "Sửa" }).first().boundingBox();
  const deleteButton = await page.getByRole("button", { name: "Xóa" }).first().boundingBox();
  expect(createButton?.height).toBeGreaterThanOrEqual(44);
  expect(editButton?.height).toBeGreaterThanOrEqual(44);
  expect(deleteButton?.height).toBeGreaterThanOrEqual(44);
  const firstRow = page.locator("tbody tr").first();
  const rowEdit = await firstRow.getByRole("button", { name: "Sửa" }).boundingBox();
  const rowDelete = await firstRow.getByRole("button", { name: "Xóa" }).boundingBox();
  expect(rowEdit).not.toBeNull();
  expect(rowDelete).not.toBeNull();
  expect(Math.abs(rowEdit!.y - rowDelete!.y)).toBeLessThan(3);
});

test("category creation action has visible background and text colors", async ({ page }) => {
  await signInAsByApi(page, "admin@example.com");
  await page.goto("/dashboard/categories");

  const createButton = page.getByRole("button", { name: "Tạo danh mục" });
  await expect(createButton).toHaveCSS("background-color", "rgb(183, 62, 45)");
  await expect(createButton).toHaveCSS("color", "rgb(255, 253, 248)");
});

test("shows category images and an accessible placeholder when no image exists", async ({ page }) => {
  await page.route("**/api/v1/categories**", (route) => route.fulfill({
    json: {
      ...categoriesResponse,
      items: [
        { ...category, imageUrl: "https://storage.example.test/categories/mon-canh.webp" },
        { ...category, id: "category-no-image", name: "Món sáng", slug: "mon-sang", imageUrl: null },
      ],
      totalCount: 2,
    },
  }));

  await page.goto("/categories");

  await expect(page.getByRole("img", { name: "Món canh" })).toHaveAttribute(
    "src",
    "https://storage.example.test/categories/mon-canh.webp",
  );
  await expect(page.getByRole("img", { name: /Món sáng.*chưa có ảnh/i })).toBeVisible();
});

test("creates a category then uploads its selected image through the authenticated BFF", async ({ page }) => {
  await signInAsByApi(page, "admin@example.com");
  await clearMockApiRequests(page);
  await page.goto("/dashboard/categories");
  await page.getByLabel("Tên danh mục").fill("Món canh mới");
  await page.locator("#categoryImageFile").setInputFiles({
    name: "mon-canh.webp",
    mimeType: "image/webp",
    buffer: Buffer.from("sample-image"),
  });
  await expect(page.getByRole("img", { name: "Món canh mới" })).toBeVisible();
  const createResponse = page.waitForResponse((response) =>
    new URL(response.url()).pathname === "/api/backend/categories" &&
    response.request().method() === "POST",
  );
  const imageResponse = page.waitForResponse((response) =>
    new URL(response.url()).pathname === "/api/backend/categories/category-created/image" &&
    response.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Tạo danh mục" }).click();

  expect((await createResponse).status()).toBe(201);
  expect((await imageResponse).status()).toBe(200);
  await expect(page.locator("[role='status']").filter({ hasText: "Đã tạo danh mục." })).toBeVisible();
  const requests = await getMockApiRequests(page);
  expect(requests.find((request) =>
    request.method === "POST" && request.path === "/api/v1/categories",
  )).toMatchObject({
    authorizationPresent: true,
    body: { name: "Món canh mới", description: null },
  });
  expect(requests.find((request) =>
    request.method === "POST" && request.path === "/api/v1/categories/category-created/image",
  )).toMatchObject({
    authorizationPresent: true,
    contentType: expect.stringContaining("multipart/form-data; boundary="),
  });
});

test("uploads a selected image when editing an existing category", async ({ page }) => {
  await signInAsByApi(page, "admin@example.com");
  await clearMockApiRequests(page);
  await page.goto("/dashboard/categories");
  await page.getByRole("button", { name: "Sửa" }).first().click();
  await expect(page.getByLabel("Tên danh mục")).toHaveValue("Món chính");
  await page.locator("#categoryImageFile").setInputFiles({
    name: "mon-chinh.webp",
    mimeType: "image/webp",
    buffer: Buffer.from("sample-image"),
  });
  await expect(page.getByRole("img", { name: "Món chính" })).toBeVisible();
  await expect(page.getByRole("img", { name: "Món chính" })).toHaveCSS("width", "320px");
  await expect(page.getByRole("img", { name: "Món chính" })).toHaveCSS("height", "200px");
  await page.getByRole("button", { name: "Cập nhật" }).click();

  await expect(page.locator("[role='status']").filter({ hasText: "Đã cập nhật danh mục." })).toBeVisible();
  const requests = await getMockApiRequests(page);
  expect(requests.find((request) =>
    request.method === "PUT" && request.path === "/api/v1/categories/category-1",
  )?.body).toMatchObject({
    name: "Món chính",
    imageUrl: null,
  });
  expect(requests.find((request) =>
    request.method === "POST" && request.path === "/api/v1/categories/category-1/image",
  )).toMatchObject({
    authorizationPresent: true,
    contentType: expect.stringContaining("multipart/form-data; boundary="),
  });
});

test("retries a failed category image upload without creating a duplicate category", async ({ page }) => {
  await signInAsByApi(page, "admin@example.com");
  await clearMockApiRequests(page);
  await queueMockApiResponse(page, {
    method: "POST",
    path: "/api/v1/categories/category-created/image",
    status: 503,
    body: { title: "Storage unavailable" },
  });
  await page.goto("/dashboard/categories");
  await page.getByLabel("Tên danh mục").fill("Món canh mới");
  await page.locator("#categoryImageFile").setInputFiles({
    name: "mon-canh.webp",
    mimeType: "image/webp",
    buffer: Buffer.from("sample-image"),
  });
  await page.getByRole("button", { name: "Tạo danh mục" }).click();
  await expect(page.locator("[data-error-summary]")).toContainText(/đã tạo danh mục.*ảnh chưa tải lên/i);
  await page.getByRole("button", { name: "Thử tải ảnh lại" }).click();

  await expect(page.locator("[role='status']").filter({ hasText: "Đã tạo danh mục." })).toBeVisible();
  const requests = await getMockApiRequests(page);
  expect(requests.filter((request) =>
    request.method === "POST" && request.path === "/api/v1/categories",
  )).toHaveLength(1);
  expect(requests.filter((request) =>
    request.method === "POST" && request.path === "/api/v1/categories/category-created/image",
  )).toHaveLength(2);
});
