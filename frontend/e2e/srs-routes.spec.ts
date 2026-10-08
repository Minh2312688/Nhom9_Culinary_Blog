import { expect, test } from "@playwright/test";

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
  images: [],
  nutrition: null,
};

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    sessionStorage.setItem("culinary_access_token", "e2e-token");
    sessionStorage.setItem("culinary_refresh_token", "e2e-refresh");
  });

  await page.route("**/api/v1/auth/me", route => route.fulfill({
    json: {
      id: "author-1",
      email: "chef@example.com",
      displayName: "Đầu bếp",
      avatarUrl: null,
      bio: null,
      emailConfirmed: true,
      createdAt: "2026-10-06T00:00:00Z",
      roles: ["Admin"],
    },
  }));
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
  const routes = [
    "/",
    "/recipes",
    "/recipes/pho-bo",
    "/categories",
    "/categories/mon-viet",
    "/dashboard",
    "/dashboard/recipes",
    "/dashboard/recipes/new",
    "/dashboard/recipes/00000000-0000-0000-0000-000000000001/edit",
    "/dashboard/categories",
  ];

  for (const route of routes) {
    const response = await page.goto(route);
    expect(response?.status(), route).toBe(200);
    await expect(page.locator("main"), route).toBeVisible();
  }
});

test("home renders recipes returned by the API", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("link", { name: /Phở bò/ })).toBeVisible();
  await expect(page.getByRole("link", { name: /Phở bò/ })).toHaveAttribute("href", "/recipes/pho-bo");
});

test("recipe detail renders ingredients and preparation steps", async ({ page }) => {
  await page.goto("/recipes/pho-bo");

  await expect(page.getByRole("heading", { name: "Phở bò" })).toBeVisible();
  await expect(page.getByText("Thịt bò", { exact: false })).toBeVisible();
  await expect(page.getByText("Nấu nước dùng.")).toBeVisible();
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

  await page.goto("/dashboard/recipes");
  await page.getByRole("button", { name: "Trang sau" }).click();
  await expect(page).toHaveURL(/dashboard\/recipes\?page=2/);
  await expect.poll(() => requests.at(-1)?.searchParams.get("page")).toBe("2");
  expect(requests.at(-1)?.searchParams.get("pageSize")).toBe("12");
});

test("new recipe is submitted to the API", async ({ page }) => {
  let submittedRecipe: Record<string, unknown> | undefined;
  await page.route("**/api/v1/recipes/", async route => {
    if (route.request().method() !== "POST") {
      await route.fallback();
      return;
    }
    submittedRecipe = route.request().postDataJSON();
    await route.fulfill({ status: 201, json: recipe });
  });

  await page.goto("/dashboard/recipes/new");
  await page.getByLabel("Tên món").fill("Bún bò Huế");
  await page.getByLabel("Danh mục").selectOption(category.id);
  await page.getByLabel("Nguyên liệu (mỗi dòng: tên | số lượng | đơn vị | ghi chú)").fill("Bắp bò | 300 | g");
  await page.getByLabel("Các bước chế biến (mỗi bước một dòng)").fill("Nấu nước dùng.");
  await page.getByRole("button", { name: "Lưu bản nháp" }).click();

  await expect.poll(() => submittedRecipe).toMatchObject({
    title: "Bún bò Huế",
    categoryId: category.id,
    ingredients: [{ name: "Bắp bò", quantity: 300, unit: "g" }],
    steps: [{ description: "Nấu nước dùng." }],
  });
});

test("editing a recipe sends its concurrency token", async ({ page }) => {
  let updatePayload: Record<string, unknown> | undefined;
  await page.route(`**/api/v1/recipes/${recipe.id}`, async route => {
    if (route.request().method() !== "PUT") {
      await route.fallback();
      return;
    }
    updatePayload = route.request().postDataJSON();
    await route.fulfill({ status: 200, json: recipe });
  });

  await page.goto(`/dashboard/recipes/${recipe.id}/edit`);
  await page.getByLabel("Tên món").fill("Phở bò gia truyền");
  await page.getByRole("button", { name: "Lưu bản nháp" }).click();

  await expect.poll(() => updatePayload).toMatchObject({
    title: "Phở bò gia truyền",
    rowVersion: recipe.rowVersion,
  });
});

test("category management submits an authenticated create request", async ({ page }) => {
  let submittedCategory: Record<string, unknown> | undefined;
  let authorization: string | undefined;
  await page.route("**/api/v1/categories/", async route => {
    if (route.request().method() !== "POST") {
      await route.fallback();
      return;
    }
    submittedCategory = route.request().postDataJSON();
    authorization = route.request().headers().authorization;
    await route.fulfill({ status: 201, json: category });
  });

  await page.goto("/dashboard/categories");
  await page.getByLabel("Tên danh mục").fill("Món chay");
  await page.getByLabel("Mô tả").fill("Công thức không thịt");
  await page.getByRole("button", { name: "Tạo danh mục" }).click();

  await expect.poll(() => submittedCategory).toEqual({
    name: "Món chay",
    description: "Công thức không thịt",
  });
  expect(authorization).toBe("Bearer e2e-token");
});
