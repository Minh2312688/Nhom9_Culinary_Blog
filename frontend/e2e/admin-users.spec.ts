import { expect, test } from "@playwright/test";
import { signInAsByApi } from "./support/auth";
import {
  clearMockApiRequests,
  getMockApiRequests,
  queueMockApiResponse,
} from "./support/mock-api";

const users = [
  {
    id: "admin-1",
    email: "admin@example.com",
    displayName: "Admin User",
    emailConfirmed: true,
    createdAt: "2026-01-10T10:00:00Z",
    isActive: true,
    isLockedOut: false,
    roles: ["Admin"],
  },
  {
    id: "author-1",
    email: "author@example.com",
    displayName: "Author User",
    emailConfirmed: true,
    createdAt: "2026-02-10T10:00:00Z",
    isActive: true,
    isLockedOut: false,
    roles: ["Author"],
  },
  {
    id: "reader-1",
    email: "reader@example.com",
    displayName: "Reader User",
    emailConfirmed: false,
    createdAt: "2026-03-10T10:00:00Z",
    isActive: true,
    isLockedOut: false,
    roles: ["Reader"],
  },
];

test.beforeEach(async ({ page }) => {
  const reset = await page.context().request.post("http://127.0.0.1:5055/__test/auth/reset");
  expect(reset.status()).toBe(204);
  await clearMockApiRequests(page);
});

test("Admin can view all users and roles from the dashboard navigation", async ({ page }) => {
  await signInAsByApi(page, "admin@example.com");
  await queueMockApiResponse(page, { method: "GET", path: "/api/v1/admin/users", status: 200, body: users });

  await page.goto("/dashboard/users");

  await expect(page.getByRole("heading", { level: 1, name: "Quản lý người dùng" })).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Điều hướng bảng điều khiển" })
    .getByRole("link", { name: "Người dùng" })).toBeVisible();
  await expect(page.getByRole("row", { name: /Admin User.*admin@example.com.*Quản trị viên/ })).toBeVisible();
  await expect(page.getByRole("row", { name: /Author User.*author@example.com.*Tác giả/ })).toBeVisible();
  await expect(page.getByRole("row", { name: /Reader User.*reader@example.com.*Độc giả/ })).toBeVisible();
});

test("Admin dashboard shows system management shortcuts instead of author actions", async ({ page }) => {
  await signInAsByApi(page, "admin@example.com");
  await page.goto("/dashboard");

  const dashboard = page.locator("main");
  await expect(dashboard.getByText("QUẢN TRỊ HỆ THỐNG")).toBeVisible();
  await expect(dashboard.getByRole("link", { name: /Công thức/ })).toHaveAttribute("href", "/dashboard/recipes");
  await expect(dashboard.getByRole("link", { name: /Danh mục/ })).toHaveAttribute("href", "/dashboard/categories");
  await expect(dashboard.getByRole("link", { name: /Người dùng/ })).toHaveAttribute("href", "/dashboard/users");
  await expect(dashboard.getByRole("link", { name: /Tạo công thức/ })).toHaveCount(0);
});

test("Admin can open the recipe authoring form", async ({ page }) => {
  await signInAsByApi(page, "admin@example.com");
  await page.goto("/dashboard/recipes/new");

  await expect(page.getByRole("heading", { level: 1, name: "Tạo công thức mới" })).toBeVisible();
  await expect(page.locator("#title")).toBeVisible();
});

test("Admin can create and edit recipes from the management list", async ({ page }) => {
  await page.goto("/auth/login?callbackUrl=/dashboard/recipes");
  await queueMockApiResponse(page, {
    method: "GET",
    path: "/api/v1/admin/recipes",
    status: 200,
    body: {
      items: [{
        id: "draft-recipe",
        title: "Admin-managed draft",
        slug: "admin-managed-draft",
        description: null,
        difficulty: "Easy",
        cookTimeMinutes: 10,
        servings: 2,
        status: "Draft",
        categoryId: "category-1",
        authorId: "author-1",
        publishedAt: null,
        rowVersion: "v1",
      }],
      totalCount: 1,
      page: 1,
      pageSize: 12,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    },
  });
  await signInAsByApi(page, "admin@example.com");

  const dashboard = page.locator("main");
  const recipe = page.getByRole("row", { name: /Admin-managed draft/ });

  await expect(dashboard.getByRole("link", { name: /Tạo công thức/ })).toBeVisible();
  await expect(recipe.getByRole("link", { name: "Sửa" })).toHaveAttribute(
    "href",
    "/dashboard/recipes/draft-recipe/edit",
  );
  await expect(recipe.getByRole("button", { name: "Xuất bản" })).toBeVisible();
  const requests = await getMockApiRequests(page);
  expect(requests.find((request) => request.path === "/api/v1/admin/recipes")).toMatchObject({
    method: "GET",
    authorizationPresent: true,
  });
});

test("Admin can archive a published recipe", async ({ page }) => {
  await signInAsByApi(page, "admin@example.com");
  await queueMockApiResponse(page, {
    method: "GET",
    path: "/api/v1/admin/recipes",
    status: 200,
    times: 2,
    body: {
      items: [{
        id: "published-recipe",
        title: "Admin-managed published recipe",
        slug: "admin-managed-published-recipe",
        description: null,
        difficulty: "Easy",
        cookTimeMinutes: 10,
        servings: 2,
        status: "Published",
        categoryId: "category-1",
        authorId: "author-1",
        publishedAt: "2026-01-01T00:00:00Z",
        rowVersion: "v1",
      }],
      totalCount: 1,
      page: 1,
      pageSize: 12,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    },
  });
  await queueMockApiResponse(page, {
    method: "PATCH",
    path: "/api/v1/recipes/published-recipe/archive",
    status: 204,
  });
  await page.goto("/dashboard/recipes");

  const recipe = page.getByRole("row", { name: /Admin-managed published recipe/ });
  await recipe.getByRole("button", { name: "Lưu trữ" }).click();

  await expect(page.getByText("Đã lưu trữ công thức.")).toBeVisible();
  const requests = await getMockApiRequests(page);
  const archiveRequest = requests.find((request) =>
    request.method === "PATCH" &&
    request.path === "/api/v1/recipes/published-recipe/archive",
  );
  expect(archiveRequest).toMatchObject({
    authorizationPresent: true,
  });
});

test("Author cannot access user management", async ({ page }) => {
  await signInAsByApi(page);
  await page.goto("/dashboard/users");

  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByRole("link", { name: "Người dùng" })).toHaveCount(0);
  await expect(page.getByText("KHÔNG GIAN TÁC GIẢ")).toBeVisible();
  await expect(page.locator("main").getByRole("link", { name: /Tạo công thức/ })).toBeVisible();
});
