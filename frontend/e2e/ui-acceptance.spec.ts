import { expect, test } from "@playwright/test";

const category = {
  id: "00000000-0000-0000-0000-000000000002",
  name: "Món Việt",
  slug: "mon-viet",
  description: "Công thức Việt",
  imageUrl: null,
  orderIndex: 0,
  recipeCount: 1,
};

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    sessionStorage.setItem("culinary_access_token", "ui-acceptance-token");
  });
  await page.route("**/api/v1/categories**", route => route.fulfill({
    json: { items: [category], totalCount: 1, page: 1, pageSize: 12, totalPages: 1, hasNextPage: false, hasPreviousPage: false },
  }));
});

test("renders API validation errors beside fields and in an error summary", async ({ page }) => {
  await page.route("**/api/v1/recipes/", route => route.fulfill({
    status: 400,
    contentType: "application/problem+json",
    body: JSON.stringify({
      title: "Validation failed",
      detail: "Vui lòng kiểm tra dữ liệu công thức.",
      errors: {
        Title: ["Tiêu đề đã được sử dụng."],
        UnknownField: ["Không thể lưu trường này."],
      },
    }),
  }));

  await page.goto("/dashboard/recipes/new");
  await page.getByLabel("Tên món").fill("Bún bò Huế");
  await page.getByLabel("Danh mục").selectOption(category.id);
  await page.getByRole("button", { name: "Lưu bản nháp" }).click();

  const titleField = page.getByLabel("Tên món");
  const titleError = page.getByText("Tiêu đề đã được sử dụng.");
  await expect(titleError).toHaveAttribute("role", "alert");
  await expect(titleField).toHaveAttribute("aria-describedby", /title-error/);
  await expect(page.locator("[data-error-summary]")).toContainText("Không thể lưu trường này.");
  await expect(page.locator("[data-error-summary]")).toContainText("Vui lòng kiểm tra dữ liệu công thức.");
});

test("maps category validation errors to their associated input", async ({ page }) => {
  await page.route("**/api/v1/categories/", route => route.fulfill({
    status: 400,
    contentType: "application/problem+json",
    body: JSON.stringify({
      title: "Validation failed",
      errors: { Name: ["Tên danh mục đã tồn tại."] },
    }),
  }));

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
  await page.route("**/api/v1/recipes/?page=1&pageSize=12", route => route.fulfill({
    json: { items: [draft], totalCount: 1, page: 1, pageSize: 12, totalPages: 1, hasNextPage: false, hasPreviousPage: false },
  }));
  await page.route(`**/api/v1/recipes/${draft.id}/publish`, route => route.fulfill({
    status: 409,
    contentType: "application/problem+json",
    body: JSON.stringify({ title: "Conflict", detail: "Không thể xuất bản bản nháp này." }),
  }));

  await page.goto("/dashboard/recipes");
  await expect(page.getByText("Bản nháp", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Xuất bản" }).click();

  await expect(page.getByText("Bản nháp", { exact: true })).toBeVisible();
  await expect(page.locator("[data-error-summary]")).toContainText("Không thể xuất bản bản nháp này.");
});

test("shows accessible loading feedback while dashboard recipes are loading", async ({ page }) => {
  let releaseResponse!: () => void;
  const responseGate = new Promise<void>(resolve => {
    releaseResponse = resolve;
  });
  await page.route("**/api/v1/recipes/?page=1&pageSize=12", async route => {
    await responseGate;
    await route.fulfill({
      json: { items: [], totalCount: 0, page: 1, pageSize: 12, totalPages: 0, hasNextPage: false, hasPreviousPage: false },
    });
  });

  await page.goto("/dashboard/recipes");
  await expect(page.getByRole("status")).toHaveAttribute("aria-busy", "true");
  releaseResponse();
});

test("announces successful category creation", async ({ page }) => {
  await page.route("**/api/v1/categories/", route => {
    if (route.request().method() === "POST") {
      return route.fulfill({ status: 201, json: { ...category, name: "Món chay" } });
    }
    return route.fallback();
  });

  await page.goto("/dashboard/categories");
  await page.getByLabel("Tên danh mục").fill("Món chay");
  await page.getByRole("button", { name: "Tạo danh mục" }).click();

  await expect(page.getByRole("status")).toContainText("Đã tạo danh mục.");
});
