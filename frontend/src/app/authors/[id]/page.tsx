import type { AuthorProfilePage } from "@/lib/recipes-api";
import { getPublicAuthorProfile, PublicAuthorApiError } from "@/lib/server/authors-api";
import { SitePage, ErrorNotice } from "../../site-ui";
import styles from "../../site-ui.module.css";
import { AuthorProfileClient } from "./author-profile-client";

export default async function AuthorProfilePage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ page?: string | string[] }>;
}) {
  const [{ id }, query] = await Promise.all([params, searchParams]);
  const requestedPage = Array.isArray(query.page) ? query.page[0] : query.page;
  const parsedPage = Number(requestedPage ?? 1);
  const page = Number.isSafeInteger(parsedPage) && parsedPage > 0 ? parsedPage : 1;

  let author: AuthorProfilePage;
  try {
    author = await getPublicAuthorProfile(id, page, 12);
  } catch (error) {
    const message = error instanceof PublicAuthorApiError && error.status === 404
      ? "Không tìm thấy tác giả."
      : "Không thể tải hồ sơ tác giả. Vui lòng thử lại sau.";
    return (
      <SitePage>
        <main className={styles.content}>
          <h1>Hồ sơ tác giả</h1>
          <ErrorNotice>{message}</ErrorNotice>
        </main>
      </SitePage>
    );
  }

  return <AuthorProfileClient author={author} />;
}
