"use client";

import { createContext, useCallback, useContext, useEffect, useRef, useState } from "react";
import { ApiRequestError } from "@/lib/recipes-api";
import styles from "./site-ui.module.css";

export type FeedbackKind = "success" | "error";
export type FeedbackApi = {
  showToast(message: string, kind: FeedbackKind): void;
  dismissToast(): void;
};

export function getFormErrorDetails(error: unknown, fieldNames: readonly string[]) {
  const fieldErrors: Record<string, string[]> = {};
  const unmatched: string[] = [];
  if (!(error instanceof ApiRequestError)) {
    return {
      fieldErrors,
      summary: error instanceof Error ? error.message : "Không thể lưu dữ liệu.",
    };
  }

  for (const [apiField, messages] of Object.entries(error.problem?.errors ?? {})) {
    const normalized = apiField.toLowerCase().replace(/\[\d+\]/g, "");
    const field = fieldNames.find((name) => {
      const candidate = name.toLowerCase();
      return normalized === candidate ||
        normalized.startsWith(`${candidate}.`) ||
        normalized.endsWith(`.${candidate}`);
    });
    if (field) fieldErrors[field] = [...(fieldErrors[field] ?? []), ...messages];
    else unmatched.push(...messages);
  }

  const summary = unmatched.length
    ? [error.problem?.detail || error.problem?.title || error.message, ...unmatched].join(" ")
    : Object.keys(fieldErrors).length
      ? null
      : error.message;
  return { fieldErrors, summary };
}

const FeedbackContext = createContext<FeedbackApi | null>(null);

export function FeedbackProvider({ children }: Readonly<{ children: React.ReactNode }>) {
  const [toast, setToast] = useState<{ message: string; kind: FeedbackKind } | null>(null);
  const timeout = useRef<ReturnType<typeof setTimeout> | null>(null);

  const dismissToast = useCallback(() => {
    if (timeout.current) clearTimeout(timeout.current);
    timeout.current = null;
    setToast(null);
  }, []);

  const showToast = useCallback((message: string, kind: FeedbackKind) => {
    if (timeout.current) clearTimeout(timeout.current);
    setToast({ message, kind });
    timeout.current = setTimeout(() => {
      timeout.current = null;
      setToast(null);
    }, kind === "error" ? 8000 : 5000);
  }, []);

  useEffect(() => () => {
    if (timeout.current) clearTimeout(timeout.current);
  }, []);

  return (
    <FeedbackContext.Provider value={{ showToast, dismissToast }}>
      {children}
      {toast && (
        <div className={`${styles.toast} ${toast.kind === "success" ? styles.toastSuccess : styles.toastError}`} role={toast.kind === "success" ? "status" : "alert"}>
          <span>{toast.message}</span>
          <button type="button" className={styles.toastButton} aria-label="Đóng thông báo" onClick={dismissToast}>Đóng</button>
        </div>
      )}
    </FeedbackContext.Provider>
  );
}

export function useFeedback(): FeedbackApi {
  const feedback = useContext(FeedbackContext);
  if (!feedback) throw new Error("useFeedback must be used within FeedbackProvider.");
  return feedback;
}
