import { expect, test } from "@playwright/test";

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

test("creates a category then uploads its selected image as multipart data", async ({ page }) => {
  await page.addInitScript(() => {
    sessionStorage.setItem("culinary_access_token", "category-image-token");
  });
  let created = 0;
  let uploadHeaders: Record<string, string> | undefined;
  let uploadBody = "";
  await page.route("**/api/v1/categories**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() === "GET") {
      await route.fulfill({ json: categoriesResponse });
      return;
    }
    if (request.method() === "POST" && path === "/api/v1/categories/") {
      created++;
      await route.fulfill({ status: 201, json: { ...category, id: "category-created" } });
      return;
    }
    if (request.method() === "POST" && path.endsWith("/category-created/image")) {
      uploadHeaders = await request.allHeaders();
      uploadBody = request.postDataBuffer()?.toString("utf8") ?? "";
      await route.fulfill({
        status: 200,
        json: { ...category, id: "category-created", imageUrl: "https://storage.example.test/category.webp" },
      });
      return;
    }
    await route.fallback();
  });

  await page.goto("/dashboard/categories");
  await page.getByLabel("Tên danh mục").fill("Món canh mới");
  await page.locator("#categoryImageFile").setInputFiles({
    name: "mon-canh.webp",
    mimeType: "image/webp",
    buffer: Buffer.from("sample-image"),
  });
  await expect(page.getByRole("img", { name: "Món canh mới" })).toBeVisible();
  await page.getByRole("button", { name: "Tạo danh mục" }).click();

  await expect(page.locator("[role='status']").filter({ hasText: "Đã tạo danh mục." })).toBeVisible();
  expect(created).toBe(1);
  expect(uploadHeaders?.authorization).toBe("Bearer category-image-token");
  expect(uploadHeaders?.["content-type"]).toContain("multipart/form-data; boundary=");
  expect(uploadBody).toContain("name=\"file\"");
  expect(uploadBody).toContain("mon-canh.webp");
});

test("uploads a selected image when editing an existing category", async ({ page }) => {
  await page.addInitScript(() => {
    sessionStorage.setItem("culinary_access_token", "category-image-token");
  });
  let updatedCategory: Record<string, unknown> | undefined;
  let uploaded = false;
  await page.route("**/api/v1/categories**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() === "GET") {
      await route.fulfill({ json: categoriesResponse });
      return;
    }
    if (request.method() === "PUT" && path.endsWith(`/${category.id}`)) {
      updatedCategory = request.postDataJSON();
      await route.fulfill({ status: 200, json: category });
      return;
    }
    if (request.method() === "POST" && path.endsWith(`/${category.id}/image`)) {
      uploaded = request.headers()["content-type"]?.includes("multipart/form-data") ?? false;
      await route.fulfill({
        status: 200,
        json: { ...category, imageUrl: "https://storage.example.test/categories/mon-canh.webp" },
      });
      return;
    }
    await route.fallback();
  });

  await page.goto("/dashboard/categories");
  await page.getByRole("button", { name: "Sửa" }).click();
  await page.locator("#categoryImageFile").setInputFiles({
    name: "mon-canh.webp",
    mimeType: "image/webp",
    buffer: Buffer.from("sample-image"),
  });
  await expect(page.getByRole("img", { name: "Món canh" })).toBeVisible();
  await page.getByRole("button", { name: "Cập nhật" }).click();

  await expect(page.locator("[role='status']").filter({ hasText: "Đã cập nhật danh mục." })).toBeVisible();
  expect(updatedCategory).toMatchObject({
    name: "Món canh",
    description: "Canh thanh mát",
    imageUrl: null,
  });
  expect(uploaded).toBe(true);
});

test("retries a failed category image upload without creating a duplicate category", async ({ page }) => {
  await page.addInitScript(() => {
    sessionStorage.setItem("culinary_access_token", "category-image-token");
  });
  let createCount = 0;
  let uploadCount = 0;
  await page.route("**/api/v1/categories**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() === "GET") {
      await route.fulfill({ json: categoriesResponse });
      return;
    }
    if (request.method() === "POST" && path === "/api/v1/categories/") {
      createCount++;
      await route.fulfill({ status: 201, json: { ...category, id: "category-created" } });
      return;
    }
    if (request.method() === "POST" && path.endsWith("/category-created/image")) {
      uploadCount++;
      await route.fulfill(uploadCount === 1
        ? { status: 503, contentType: "application/problem+json", json: { title: "Storage unavailable" } }
        : { status: 200, json: { ...category, id: "category-created", imageUrl: "https://storage.example.test/category.webp" } });
      return;
    }
    await route.fallback();
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
  expect(createCount).toBe(1);
  expect(uploadCount).toBe(2);
});
