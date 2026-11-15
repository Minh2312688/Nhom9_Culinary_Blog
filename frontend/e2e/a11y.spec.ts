import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { signInAsByApi } from "./support/auth";

interface ViolationSummary {
  id: string;
  impact: string;
  nodesCount: number;
  firstSelector: string;
}

function processViolations(route: string, violations: any[]) {
  const counts = { critical: 0, serious: 0, moderate: 0, minor: 0 };
  const items: ViolationSummary[] = [];

  for (const v of violations) {
    const impact = (v.impact || "minor").toLowerCase();
    if (impact in counts) {
      counts[impact as keyof typeof counts] += 1;
    }
    items.push({
      id: v.id,
      impact: v.impact ?? "unknown",
      nodesCount: v.nodes?.length ?? 0,
      firstSelector: v.nodes?.[0]?.target?.join(" ") ?? "N/A",
    });
  }

  console.log(`\n==================================================`);
  console.log(`Accessibility Report for: ${route}`);
  console.log(`Số vi phạm theo impact:`);
  console.log(`  - Critical: ${counts.critical}`);
  console.log(`  - Serious:  ${counts.serious}`);
  console.log(`  - Moderate: ${counts.moderate}`);
  console.log(`  - Minor:    ${counts.minor}`);

  if (items.length > 0) {
    console.log(`Danh sách vi phạm (${items.length}):`);
    for (const item of items) {
      console.log(`  • [${item.impact.toUpperCase()}] ${item.id} (${item.nodesCount} node) | Selector: ${item.firstSelector}`);
    }
  } else {
    console.log(`Không phát hiện vi phạm WCAG.`);
  }
  console.log(`==================================================\n`);

  expect(
    violations,
    `Route ${route} phát hiện ${violations.length} vi phạm WCAG 2.1 A/AA:\n` +
      violations
        .map(
          (v) =>
            ` - [${v.impact.toUpperCase()}] ${v.id}: ${v.help} (Nodes: ${v.nodes?.length}, Selector: ${v.nodes?.[0]?.target?.join(" ")})`
        )
        .join("\n")
  ).toEqual([]);
}

const accessibilityRoutes = [
  { route: "/", email: undefined },
  { route: "/recipes", email: undefined },
  { route: "/recipes/pho-bo", email: undefined },
  { route: "/categories", email: undefined },
  { route: "/categories/mon-chinh", email: undefined },
  { route: "/search", email: undefined },
  { route: "/authors/author-1", email: undefined },
  { route: "/dashboard", email: "admin@example.com" },
  { route: "/dashboard/recipes", email: "admin@example.com" },
  { route: "/dashboard/recipes/new", email: "admin@example.com" },
  {
    route: "/dashboard/recipes/00000000-0000-0000-0000-000000000001/edit",
    email: "admin@example.com",
  },
  { route: "/dashboard/categories", email: "admin@example.com" },
];

test.describe("Automated Accessibility Checks", () => {
  for (const { route, email } of accessibilityRoutes) {
    test(`WCAG 2.1 A/AA check for route ${route}`, async ({ page }) => {
      if (email) await signInAsByApi(page, email);
      await page.goto(route);
      await page.locator("main").waitFor({ state: "visible" });
      await page.waitForLoadState("domcontentloaded");

      const results = await new AxeBuilder({ page })
        .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
        .analyze();

      processViolations(route, results.violations);
    });
  }

  test("keyboard navigation on home page focuses interactive elements", async ({ page }) => {
    await page.goto("/");
    await page.locator("main").waitFor({ state: "visible" });
    await page.waitForLoadState("domcontentloaded");

    const focusedElements: string[] = [];

    console.log("\n--- Keyboard Navigation Test on Home Page ---");
    for (let i = 0; i < 3; i++) {
      await page.keyboard.press("Tab");

      const activeInfo = await page.evaluate(() => {
        const el = document.activeElement;
        if (!el || el === document.body) return null;
        return {
          tagName: el.tagName,
          id: el.id || null,
          text: (el.textContent || "").trim().slice(0, 40),
          outerHTML: el.outerHTML.slice(0, 120),
        };
      });

      expect(
        activeInfo,
        `Lần nhấn Tab thứ ${i + 1} phải focus vào một phần tử tương tác, không phải body`
      ).not.toBeNull();

      console.log(`Tab ${i + 1}: <${activeInfo!.tagName}> "${activeInfo!.text}" (id: ${activeInfo!.id})`);

      const elementKey = activeInfo!.outerHTML;
      expect(
        focusedElements,
        `Lần nhấn Tab thứ ${i + 1} phải focus vào một phần tử mới, không trùng lặp`
      ).not.toContain(elementKey);

      focusedElements.push(elementKey);
    }

    expect(focusedElements.length).toBe(3);
    console.log("3 lần nhấn Tab liên tiếp đều focus vào các phần tử tương tác khác nhau.\n");
  });
});

