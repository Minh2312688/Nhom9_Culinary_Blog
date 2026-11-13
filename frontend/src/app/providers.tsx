"use client";

import { SessionProvider } from "next-auth/react";
import { FeedbackProvider } from "./feedback";

export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <SessionProvider>
      <FeedbackProvider>{children}</FeedbackProvider>
    </SessionProvider>
  );
}
