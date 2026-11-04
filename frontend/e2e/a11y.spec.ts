import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

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

  const criticalOrSerious = violations.filter(
    (v) => v.impact === "critical" || v.impact === "serious"
  );

  expect(
    criticalOrSerious,
    `Route ${route} phát hiện ${criticalOrSerious.length} vi phạm accessibility mức critical hoặc serious:\n` +
      criticalOrSerious
        .map(
          (v) =>
            ` - [${v.impact.toUpperCase()}] ${v.id}: ${v.help} (Nodes: ${v.nodes?.length}, Selector: ${v.nodes?.[0]?.target?.join(" ")})`
        )
        .join("\n")
  ).toEqual([]);
}

test.describe("Automated Accessibility Checks", () => {
  test("accessibility check for route /", async ({ page }) => {
    await page.goto("/");
    await page.locator("main").waitFor({ state: "visible" });
    await page.waitForLoadState("domcontentloaded");

    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
      .analyze();

    processViolations("/", results.violations);
  });

  test("accessibility check for route /recipes", async ({ page }) => {
    await page.goto("/recipes");
    await page.locator("main").waitFor({ state: "visible" });
    await page.waitForLoadState("domcontentloaded");

    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
      .analyze();

    processViolations("/recipes", results.violations);
  });

  test("accessibility check for route /recipes/pho-bo", async ({ page }) => {
    await page.goto("/recipes/pho-bo");
    await page.locator("main").waitFor({ state: "visible" });
    await page.waitForLoadState("domcontentloaded");

    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
      .analyze();

    processViolations("/recipes/pho-bo", results.violations);
  });

  test("accessibility check for route /search", async ({ page }) => {
    await page.goto("/search");
    await page.locator("main").waitFor({ state: "visible" });
    await page.waitForLoadState("domcontentloaded");

    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
      .analyze();

    processViolations("/search", results.violations);
  });

  test("accessibility check for route /dashboard", async ({ page }) => {
    await page.goto("/dashboard");
    await page.locator("main").waitFor({ state: "visible" });
    await page.waitForLoadState("domcontentloaded");

    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
      .analyze();

    processViolations("/dashboard", results.violations);
  });

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

