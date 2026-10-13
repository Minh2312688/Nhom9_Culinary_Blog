import { test, expect } from "@playwright/test";
import { execSync } from "child_process";

test.describe("NFR-SEC-004: Content Security Policy Verification", () => {
  test("production CSP must not contain unsafe-eval or localhost, and must support Google GIS", async () => {
    const rawOutput = execSync(
      `node -e "process.env.NODE_ENV='production'; import('./next.config.mjs').then(m => m.default.headers()).then(h => console.log(JSON.stringify(h)))"`,
      { cwd: process.cwd() }
    ).toString();

    const headers = JSON.parse(rawOutput);
    const cspEntry = headers[0]?.headers?.find(
      (h: { key: string; value: string }) => h.key === "Content-Security-Policy"
    );

    expect(cspEntry).toBeDefined();
    const csp = cspEntry.value;

    // Strict production security invariants
    expect(csp).not.toContain("unsafe-eval");
    expect(csp).not.toContain("localhost");
    expect(csp).not.toContain("127.0.0.1");

    // Standard structural protection
    expect(csp).toContain("default-src 'self'");
    expect(csp).toContain("object-src 'none'");
    expect(csp).toContain("frame-ancestors 'none'");
    expect(csp).toContain("base-uri 'self'");

    // Google Identity Services compatibility
    expect(csp).toContain("https://accounts.google.com");
    expect(csp).toContain("https://*.googleusercontent.com");
  });

  test("development CSP must permit local HMR, WebSocket, and localhost origins", async () => {
    const rawOutput = execSync(
      `node -e "process.env.NODE_ENV='development'; import('./next.config.mjs').then(m => m.default.headers()).then(h => console.log(JSON.stringify(h)))"`,
      { cwd: process.cwd() }
    ).toString();

    const headers = JSON.parse(rawOutput);
    const cspEntry = headers[0]?.headers?.find(
      (h: { key: string; value: string }) => h.key === "Content-Security-Policy"
    );

    expect(cspEntry).toBeDefined();
    const devCsp = cspEntry.value;

    expect(devCsp).toContain("unsafe-eval");
    expect(devCsp).toContain("localhost");
    expect(devCsp).toContain("ws:");
  });
});
