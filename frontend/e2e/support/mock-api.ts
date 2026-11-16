import { expect, type Page } from "@playwright/test";

const mockApiUrl = "http://127.0.0.1:5055";

export interface MockApiResponse {
  method: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  path: string;
  status: number;
  body?: unknown;
  times?: number;
  delayMs?: number;
}

export interface MockApiRequest {
  method: string;
  path: string;
  query: string;
  authorizationPresent: boolean;
  contentType?: string;
  body?: unknown;
}

export async function clearMockApiRequests(page: Page): Promise<void> {
  const response = await page.context().request.delete(`${mockApiUrl}/__test__/requests`);
  expect(response.status()).toBe(204);
}

export async function queueMockApiResponse(
  page: Page,
  configuration: MockApiResponse,
): Promise<void> {
  const response = await page.context().request.post(`${mockApiUrl}/__test/responses`, {
    data: configuration,
  });
  expect(response.ok()).toBe(true);
}

export async function getMockApiRequests(page: Page): Promise<MockApiRequest[]> {
  const response = await page.context().request.get(`${mockApiUrl}/__test__/requests`);
  expect(response.ok()).toBe(true);
  return response.json() as Promise<MockApiRequest[]>;
}

export async function setMockProfile(
  page: Page,
  email: string,
  profile: Record<string, unknown>,
): Promise<void> {
  const response = await page.context().request.post(`${mockApiUrl}/__test/auth/profile`, {
    data: { email, profile },
  });
  expect(response.ok()).toBe(true);
}
