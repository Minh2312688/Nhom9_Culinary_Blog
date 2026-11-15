import { expect, test } from "@playwright/test";
import { signInAsByApi } from "./support/auth";
import {
  clearMockApiRequests,
  getMockApiRequests,
  queueMockApiResponse,
} from "./support/mock-api";

const category = {
  id: "00000000-0000-0000-0000-000000000002",
  name: "Món Việt",
  slug: "mon-viet",
  description: "Công thức Việt",
  imageUrl: null,
  orderIndex: 0,
  createdAt: "2026-10-06T00:00:00Z",
  recipeCount: 1,
};

const recipe = {
  id: "00000000-0000-0000-0000-000000000001",
  title: "Phở bò",
  slug: "pho-bo",
  description: "Nước dùng đậm đà",
  difficulty: "Medium",
  prepTimeMinutes: 20,
  cookTimeMinutes: 90,
  servings: 4,
  status: 1,
  categoryId: category.id,
  authorId: "author-1",
  rowVersion: "AAAAAA==",
  ingredients: [{ id: "ingredient-1", name: "Thịt bò", quantity: 300, unit: "g", notes: null, orderIndex: 0 }],
  steps: [{ id: "step-1", stepNumber: 1, title: null, description: "Nấu nước dùng.", durationMinutes: 10, imageUrl: null }],
  images: [{
    id: "image-1",
    originalUrl: "https://storage.example.test/original.avif",
    mediumUrl: null,
    thumbnailUrl: null,
    altText: "Phở bò",
    isPrimary: true,
    orderIndex: 0,
  }],
  nutrition: null,
};

test.beforeEach(async ({ page }) => {
  const reset = await page.context().request.post("http://127.0.0.1:5055/__test/auth/reset");
  expect(reset.status()).toBe(204);
  await signInAsByApi(page);
  await clearMockApiRequests(page);

  await page.route("**/api/v1/categories**", route => route.fulfill({
    json: { items: [category], totalCount: 1, page: 1, pageSize: 12, totalPages: 1, hasNextPage: false, hasPreviousPage: false },
  }));
  await page.route("**/api/v1/categories/mon-viet", route => route.fulfill({ json: category }));
  await page.route("**/api/v1/recipes/**", route => route.fulfill({
    json: { items: [recipe], totalCount: 1, page: 1, pageSize: 12, totalPages: 1, hasNextPage: false, hasPreviousPage: false },
  }));
  await page.route("**/api/v1/recipes/pho-bo", route => route.fulfill({ json: recipe }));
});

test("all SRS screens have real routes", async ({ page }) => {
  test.setTimeout(90_000);
  await signInAsByApi(page, "admin@example.com");

  const routes = [
    "/",
    "/recipes",
    "/recipes/pho-bo",
    "/categories",
    "/categories/mon-viet",
    "/search",
    "/authors/author-1",
    "/dashboard",
    "/dashboard/recipes",
    "/dashboard/recipes/new",
    "/dashboard/recipes/00000000-0000-0000-0000-000000000001/edit",
    "/dashboard/categories",
  ];

  for (const route of routes) {
    const response = await page.goto(route);
    expect(response?.status(), route).toBe(200);
    await expect(page.locator("main"), route).toHaveCount(1);
    await expect(page.locator("main"), route).toBeVisible();
  }
});

test("public and dashboard routes use their shared navigation shells", async ({ page }) => {
  test.setTimeout(90_000);

  for (const route of ["/", "/recipes", "/recipes/pho-bo", "/categories", "/categories/mon-viet", "/search", "/authors/author-1"]) {
    await page.goto(route, { waitUntil: "domcontentloaded" });
    await expect(page.getByRole("navigation", { name: "Điều hướng chính" }), route).toBeVisible();
    await expect(page.getByRole("contentinfo"), route).toBeVisible();
    await expect(page.locator("main"), route).toHaveCount(1);
    if (route === "/search") {
      await expect(page.getByRole("contentinfo")).toContainText("Một công thức hay bắt đầu từ nguyên liệu đúng.");
    }
  }

  await signInAsByApi(page, "admin@example.com");
  for (const [route, activeLink] of [
    ["/dashboard", "Tổng quan"],
    ["/dashboard/recipes", "Công thức"],
    ["/dashboard/recipes/new", "Công thức"],
    ["/dashboard/recipes/00000000-0000-0000-0000-000000000001/edit", "Công thức"],
    ["/dashboard/categories", "Danh mục"],
  ]) {
    await page.goto(route, { waitUntil: "domcontentloaded" });
    const dashboardNavigation = page.getByRole("navigation", { name: "Điều hướng bảng điều khiển" });
    await expect(dashboardNavigation, route).toBeVisible();
    await expect(dashboardNavigation.getByRole("link", { name: activeLink })).toHaveAttribute("aria-current", "page");
    await expect(page.getByRole("link", { name: "Về trang chủ" })).toHaveAttribute("href", "/");
    await expect(page.getByRole("navigation", { name: "Điều hướng chính" })).toHaveCount(0);
    await expect(page.getByRole("contentinfo")).toHaveCount(0);
    await expect(page.locator("main"), route).toHaveCount(1);
  }

  await page.goto("/dashboard", { waitUntil: "domcontentloaded" });
  await page.keyboard.press("Tab");
  await expect(page.getByRole("link", { name: "Culinary Blog, trang chủ" })).toBeFocused();
  await expect(page.getByRole("link", { name: "Culinary Blog, trang chủ" })).toHaveCSS("outline-style", "solid");
});

test("home renders recipes returned by the API", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("link", { name: /Phở bò/ })).toBeVisible();
  await expect(page.getByRole("link", { name: /Phở bò/ })).toHaveAttribute("href", "/recipes/pho-bo");
});

test("recipe detail renders ingredients and preparation steps", async ({ page }) => {
  await page.goto("/recipes/pho-bo");

  const main = page.locator("main");
  await expect(main).toHaveCount(1);
  await expect(main.getByRole("heading", { name: "Phở bò" })).toBeVisible();
  await expect(main.getByText("Thịt bò", { exact: false })).toBeVisible();
  await expect(main.getByText("Nấu nước dùng.")).toBeVisible();
});

test("recipe detail falls back to original image when variants are unavailable", async ({ page }) => {
  await page.goto("/recipes/pho-bo");

  await expect(page.getByRole("img", { name: "Phở bò" }))
    .toHaveAttribute("src", "https://storage.example.test/original.avif");
});

test("search sorting is sent to the API and resets pagination", async ({ page }) => {
  const requests: URL[] = [];
  await page.route("**/api/v1/recipes/search**", async route => {
    requests.push(new URL(route.request().url()));
    await route.fulfill({
      json: { items: [recipe], totalCount: 25, page: 1, pageSize: 12, totalPages: 3, hasNextPage: true, hasPreviousPage: false },
    });
  });

  await page.goto("/search?page=3&q=pho&sortBy=createdAt&sortOrder=asc");
  await expect.poll(() => requests.at(-1)?.searchParams.get("page")).toBe("3");
  await page.getByLabel("Sắp xếp theo").selectOption("cookTime");
  await page.getByLabel("Thứ tự").selectOption("asc");

  await expect.poll(() => {
    const latest = requests.at(-1)?.searchParams;
    return [latest?.get("sortBy"), latest?.get("sortOrder"), latest?.get("page"), latest?.get("q")];
  }).toEqual(["cookTime", "asc", "1", "pho"]);
  await expect(page).toHaveURL(/sortBy=cookTime/);
  await expect(page).toHaveURL(/sortOrder=asc/);

  await page.getByRole("button", { name: "Trang sau" }).click();
  await expect.poll(() => {
    const latest = requests.at(-1)?.searchParams;
    return [latest?.get("page"), latest?.get("q"), latest?.get("categoryId"), latest?.get("sortBy"), latest?.get("sortOrder")];
  }).toEqual(["2", "pho", null, "cookTime", "asc"]);
});

test("recipe and category listings paginate through the API", async ({ page }) => {
  test.setTimeout(90_000);
  const requests: URL[] = [];
  await page.route("**/api/v1/recipes/**", async route => {
    const url = new URL(route.request().url());
    requests.push(url);
    const currentPage = Number(url.searchParams.get("page") ?? 1);
    await route.fulfill({
      json: {
        items: [recipe], totalCount: 13, page: currentPage, pageSize: 12, totalPages: 2,
        hasNextPage: currentPage === 1, hasPreviousPage: currentPage > 1,
      },
    });
  });

  await page.goto("/recipes");
  await page.getByRole("button", { name: "Trang sau" }).click();
  await expect(page).toHaveURL(/page=2/);
  await expect.poll(() => requests.at(-1)?.searchParams.get("page")).toBe("2");
  expect(requests.at(-1)?.searchParams.get("pageSize")).toBe("12");

  await page.goto("/categories/mon-viet");
  await page.getByRole("button", { name: "Trang sau" }).click();
  await expect(page).toHaveURL(/page=2/);
  await expect.poll(() => requests.at(-1)?.searchParams.get("page")).toBe("2");
  expect(requests.at(-1)?.searchParams.get("categoryId")).toBe(category.id);
  expect(requests.at(-1)?.searchParams.get("pageSize")).toBe("12");

  await clearMockApiRequests(page);
  const dashboardPage = {
    items: [recipe],
    totalCount: 13,
    pageSize: 12,
    totalPages: 2,
    hasNextPage: true,
    hasPreviousPage: false,
  };
  await queueMockApiResponse(page, {
    method: "GET",
    path: "/api/v1/recipes/mine",
    status: 200,
    body: { ...dashboardPage, page: 1 },
  });
  await queueMockApiResponse(page, {
    method: "GET",
    path: "/api/v1/recipes/mine",
    status: 200,
    body: { ...dashboardPage, page: 2, hasNextPage: false, hasPreviousPage: true },
  });
  await page.goto("/dashboard/recipes");
  await page.getByRole("button", { name: "Trang sau" }).click();
  await expect(page).toHaveURL(/dashboard\/recipes\?page=2/);
  await expect.poll(async () => {
    const records = await getMockApiRequests(page);
    return records.at(-1)?.query;
  }).toContain("page=2");
  const records = await getMockApiRequests(page);
  expect(records.at(-1)).toMatchObject({
    method: "GET",
    path: "/api/v1/recipes/mine",
    authorizationPresent: true,
  });
  expect(new URLSearchParams(records.at(-1)?.query).get("pageSize")).toBe("12");
});

test("new recipe is submitted to the API", async ({ page }) => {
  await clearMockApiRequests(page);
  await page.goto("/dashboard/recipes/new");
  await page.getByLabel("Tiêu đề").fill("Bún bò Huế");
  await page.getByLabel("Danh mục").selectOption(category.id);
  await page.getByRole("button", { name: "Tiếp theo" }).click();
  await page.getByRole("button", { name: "Thêm nguyên liệu" }).click();
  await page.getByRole("textbox", { name: "Tên nguyên liệu 1" }).fill("Bắp bò");
  await page.getByRole("textbox", { name: "Số lượng nguyên liệu 1" }).fill("300");
  await page.getByRole("textbox", { name: "Đơn vị nguyên liệu 1" }).fill("g");
  await page.getByRole("button", { name: "Tiếp theo" }).click();
  await page.getByLabel("Các bước chế biến (mỗi bước một dòng)").fill("Nấu nước dùng.");
  const createResponse = page.waitForResponse((response) =>
    response.url().includes("/api/backend/recipes") &&
    response.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Xem lại" }).click();
  await page.getByRole("button", { name: "Tạo bản nháp" }).click();

  const response = await createResponse;
  expect(response.status()).toBe(201);
  await expect(response.json()).resolves.toMatchObject({
    title: "Bún bò Huế",
    categoryId: category.id,
    ingredients: [{ name: "Bắp bò", quantity: 300, unit: "g" }],
    steps: [{ description: "Nấu nước dùng." }],
  });
});

test("editing a recipe sends its concurrency token", async ({ page }) => {
  await clearMockApiRequests(page);
  await queueMockApiResponse(page, {
    method: "GET",
    path: "/api/v1/recipes",
    status: 200,
    body: { items: [recipe], totalCount: 1, page: 1, pageSize: 50, totalPages: 1, hasNextPage: false, hasPreviousPage: false },
  });
  await queueMockApiResponse(page, {
    method: "GET",
    path: `/api/v1/recipes/${recipe.id}`,
    status: 200,
    body: recipe,
  });
  await queueMockApiResponse(page, {
    method: "PUT",
    path: `/api/v1/recipes/${recipe.id}`,
    status: 200,
    body: recipe,
  });

  await page.goto(`/dashboard/recipes/${recipe.id}/edit`);
  await page.locator("#title").fill("Phở bò gia truyền");
  await page.getByRole("button", { name: "Tiếp theo" }).click();
  await expect(page.getByRole("textbox", { name: "Tên nguyên liệu 1" })).toHaveValue("Thịt bò");
  await expect(page.getByRole("textbox", { name: "Số lượng nguyên liệu 1" })).toHaveValue("300");
  await expect(page.locator("#ingredients")).toHaveCount(0);
  await page.getByRole("button", { name: "Tiếp theo" }).click();
  await page.getByRole("button", { name: "Xem lại" }).click();
  const updateResponse = page.waitForResponse((response) =>
    response.url().includes(`/api/backend/recipes/${recipe.id}`) &&
    response.request().method() === "PUT",
  );
  await page.getByRole("button", { name: "Cập nhật công thức" }).click();

  expect((await updateResponse).status()).toBe(200);
  const requests = await getMockApiRequests(page);
  expect(requests.find((request) =>
    request.method === "PUT" && request.path === `/api/v1/recipes/${recipe.id}`,
  )).toMatchObject({
    authorizationPresent: true,
    body: { title: "Phở bò gia truyền", rowVersion: recipe.rowVersion },
  });
});

test("category management submits an authenticated create request", async ({ page }) => {
  await signInAsByApi(page, "admin@example.com");
  await clearMockApiRequests(page);
  await queueMockApiResponse(page, {
    method: "POST",
    path: "/api/v1/categories",
    status: 201,
    body: category,
  });

  await page.goto("/dashboard/categories");
  await page.getByLabel("Tên danh mục").fill("Món chay");
  await page.getByLabel("Mô tả").fill("Công thức không thịt");
  const createResponse = page.waitForResponse((response) =>
    response.url().includes("/api/backend/categories") &&
    response.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Tạo danh mục" }).click();

  expect((await createResponse).status()).toBe(201);
  const requests = await getMockApiRequests(page);
  expect(requests.find((request) =>
    request.method === "POST" && request.path === "/api/v1/categories",
  )).toMatchObject({
    authorizationPresent: true,
    body: {
      name: "Món chay",
      description: "Công thức không thịt",
    },
  });
});
