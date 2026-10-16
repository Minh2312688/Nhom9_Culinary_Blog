import "server-only";
import type { AuthorProfilePage } from "../recipes-api";

const API_BASE_URL =
  process.env.API_INTERNAL_URL ||
  process.env.NEXT_PUBLIC_API_URL ||
  "http://localhost:5000";

export class PublicAuthorApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = "PublicAuthorApiError";
  }
}

export async function getPublicAuthorProfile(
  id: string,
  page: number,
  pageSize = 12,
): Promise<AuthorProfilePage> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  const response = await fetch(
    `${API_BASE_URL}/api/v1/authors/${encodeURIComponent(id)}?${query.toString()}`,
    { cache: "no-store" },
  );

  if (!response.ok) {
    const message = response.status === 404
      ? "Không tìm thấy tác giả."
      : "Không thể tải hồ sơ tác giả.";
    throw new PublicAuthorApiError(response.status, message);
  }

  return await response.json() as AuthorProfilePage;
}
