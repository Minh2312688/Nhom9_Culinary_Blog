"use client";

import { useEffect, useRef, useState } from "react";
import { SessionProvider, useSession } from "next-auth/react";
import { QueryClientProvider, useQueryClient } from "@tanstack/react-query";
import { FeedbackProvider } from "./feedback";
import { createQueryClient } from "@/lib/queries/query-client";

function SessionQueryCacheSync() {
  const { data: session, status } = useSession();
  const queryClient = useQueryClient();
  const previousUserId = useRef<string | null>(null);

  useEffect(() => {
    if (status === "loading") return;

    const userId = status === "authenticated" ? session?.user?.id ?? null : null;
    if (previousUserId.current && previousUserId.current !== userId) {
      queryClient.clear();
    }
    previousUserId.current = userId;
  }, [queryClient, session?.user?.id, status]);

  return null;
}

export function Providers({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(createQueryClient);

  return (
    <QueryClientProvider client={queryClient}>
      <SessionProvider>
        <SessionQueryCacheSync />
        <FeedbackProvider>{children}</FeedbackProvider>
      </SessionProvider>
    </QueryClientProvider>
  );
}
