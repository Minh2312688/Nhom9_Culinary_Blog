import { QueryClient } from "@tanstack/react-query";
import { ApiRequestError } from "@/lib/recipes-api";

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        retry: (failureCount, error) =>
          failureCount < 1
          && (error instanceof TypeError || (error instanceof ApiRequestError && error.status >= 500)),
      },
      mutations: {
        retry: false,
      },
    },
  });
}
