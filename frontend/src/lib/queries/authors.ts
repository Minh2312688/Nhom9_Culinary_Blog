import { useQuery } from "@tanstack/react-query";
import { getAuthorProfile, type AuthorProfilePage } from "@/lib/recipes-api";
import { queryKeys } from "./query-keys";

export function useAuthorProfile(
  id: string,
  page: number,
  initialData: AuthorProfilePage,
) {
  return useQuery({
    queryKey: queryKeys.authors.profile(id, page),
    queryFn: ({ signal }) => getAuthorProfile(id, page, signal),
    initialData,
  });
}
