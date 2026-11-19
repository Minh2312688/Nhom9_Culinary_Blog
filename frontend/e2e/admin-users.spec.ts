import { expect, test } from "@playwright/test";
import { signInAsByApi } from "./support/auth";
import { clearMockApiRequests, queueMockApiResponse } from "./support/mock-api";

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

test("Author cannot access user management", async ({ page }) => {
  await signInAsByApi(page);
  await page.goto("/dashboard/users");

  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByRole("link", { name: "Người dùng" })).toHaveCount(0);
  await expect(page.getByText("KHÔNG GIAN TÁC GIẢ")).toBeVisible();
  await expect(page.locator("main").getByRole("link", { name: /Tạo công thức/ })).toBeVisible();
});
