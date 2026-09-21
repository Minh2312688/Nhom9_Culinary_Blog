"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import Script from "next/script";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { loginApi, googleLoginApi, ApiError } from "@/lib/api";
import { Eye, EyeOff, Loader2, AlertCircle, ChefHat, CheckCircle2 } from "lucide-react";

const loginSchema = z.object({
  email: z.string().email("Địa chỉ email không hợp lệ."),
  password: z.string().min(1, "Vui lòng nhập mật khẩu."),
});

type LoginFormData = z.infer<typeof loginSchema>;

export default function LoginPage() {
  const [showPassword, setShowPassword] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);
  const [loginSuccess, setLoginSuccess] = useState(false);
  const [isGoogleSubmitting, setIsGoogleSubmitting] = useState(false);

  const googleClientId = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID;

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    mode: "onBlur",
  });

  const onSubmit = async (data: LoginFormData) => {
    setServerError(null);
    try {
      const response = await loginApi({
        email: data.email,
        password: data.password,
      });

      if (typeof window !== "undefined") {
        sessionStorage.setItem("culinary_access_token", response.accessToken);
        sessionStorage.setItem("culinary_refresh_token", response.refreshToken);
      }

      setLoginSuccess(true);
    } catch (err) {
      const apiErr = err as ApiError;
      setServerError(apiErr.detail || "Đăng nhập không thành công.");
    }
  };

  useEffect(() => {
    if (!googleClientId) return;

    const initializeGis = () => {
      const win = window as unknown as { google?: { accounts?: { id?: { initialize: Function; renderButton: Function } } } };
      if (win.google?.accounts?.id) {
        win.google.accounts.id.initialize({
          client_id: googleClientId,
          callback: async (response: { credential?: string }) => {
            if (!response.credential) {
              setServerError("Không nhận được token từ Google Identity Services.");
              return;
            }

            setServerError(null);
            setIsGoogleSubmitting(true);
            try {
              const authResult = await googleLoginApi(response.credential);
              if (typeof window !== "undefined") {
                sessionStorage.setItem("culinary_access_token", authResult.accessToken);
                sessionStorage.setItem("culinary_refresh_token", authResult.refreshToken);
              }
              setLoginSuccess(true);
            } catch (err) {
              const apiErr = err as ApiError;
              setServerError(apiErr.detail || "Xác thực với Google không thành công.");
            } finally {
              setIsGoogleSubmitting(false);
            }
          },
        });

        const container = document.getElementById("googleSignInDiv");
        if (container) {
          win.google.accounts.id.renderButton(container, {
            theme: "outline",
            size: "large",
            text: "signin_with",
            shape: "rectangular",
            width: 360,
          });
        }
      }
    };

    if ((window as unknown as { google?: { accounts?: { id?: unknown } } }).google?.accounts?.id) {
      initializeGis();
    } else {
      const timer = setInterval(() => {
        if ((window as unknown as { google?: { accounts?: { id?: unknown } } }).google?.accounts?.id) {
          initializeGis();
          clearInterval(timer);
        }
      }, 300);
      return () => clearInterval(timer);
    }
  }, [googleClientId]);

  return (
    <>
      <Script src="https://accounts.google.com/gsi/client" strategy="afterInteractive" />
      <div className="min-h-screen flex items-center justify-center p-4 sm:p-6 lg:p-8">
      <div className="w-full max-w-md bg-white rounded-2xl shadow-xl border border-gray-100 p-6 sm:p-8">
        {/* Header */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-14 h-14 rounded-2xl bg-orange-100 text-orange-600 mb-4 shadow-sm">
            <ChefHat className="w-8 h-8" aria-hidden="true" />
          </div>
          <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-gray-900">
            Đăng nhập Culinary Blog
          </h1>
          <p className="text-sm text-gray-500 mt-2">
            Chào mừng bạn quay trở lại với thế giới hương vị
          </p>
        </div>

        {/* Success State */}
        {loginSuccess ? (
          <div
            className="rounded-xl bg-green-50 border border-green-200 p-6 text-center space-y-4"
            role="status"
          >
            <CheckCircle2 className="w-12 h-12 text-green-600 mx-auto" />
            <h2 className="text-lg font-semibold text-green-900">Đăng nhập thành công!</h2>
            <p className="text-sm text-green-700">
              Bạn đã đăng nhập thành công vào hệ thống Culinary Blog.
            </p>
          </div>
        ) : (
          <div className="space-y-6">
            {/* Server Error Alert */}
            {serverError && (
              <div
                className="flex items-start gap-3 rounded-xl bg-red-50 border border-red-200 p-4 text-sm text-red-800"
                role="alert"
                aria-live="assertive"
              >
                <AlertCircle className="w-5 h-5 text-red-600 shrink-0 mt-0.5" aria-hidden="true" />
                <div className="flex-1">{serverError}</div>
              </div>
            )}

            {/* Email / Password Form */}
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" noValidate>
              {/* Email */}
              <div>
                <label htmlFor="email" className="block text-sm font-medium text-gray-700 mb-1">
                  Địa chỉ Email <span className="text-red-500">*</span>
                </label>
                <input
                  id="email"
                  type="email"
                  autoComplete="email"
                  disabled={isSubmitting}
                  aria-invalid={errors.email ? "true" : "false"}
                  aria-describedby={errors.email ? "email-error" : undefined}
                  className={`w-full px-4 py-2.5 rounded-xl border bg-gray-50/50 text-gray-900 text-sm transition focus:bg-white focus:outline-none focus:ring-2 ${
                    errors.email
                      ? "border-red-400 focus:ring-red-500"
                      : "border-gray-200 focus:ring-orange-500 focus:border-orange-500"
                  }`}
                  placeholder="chef@example.com"
                  {...register("email")}
                />
                {errors.email && (
                  <p id="email-error" className="mt-1.5 text-xs text-red-600" role="alert">
                    {errors.email.message}
                  </p>
                )}
              </div>

              {/* Password */}
              <div>
                <label
                  htmlFor="password"
                  className="block text-sm font-medium text-gray-700 mb-1"
                >
                  Mật khẩu <span className="text-red-500">*</span>
                </label>
                <div className="relative">
                  <input
                    id="password"
                    type={showPassword ? "text" : "password"}
                    autoComplete="current-password"
                    disabled={isSubmitting}
                    aria-invalid={errors.password ? "true" : "false"}
                    aria-describedby={errors.password ? "password-error" : undefined}
                    className={`w-full px-4 py-2.5 pr-11 rounded-xl border bg-gray-50/50 text-gray-900 text-sm transition focus:bg-white focus:outline-none focus:ring-2 ${
                      errors.password
                        ? "border-red-400 focus:ring-red-500"
                        : "border-gray-200 focus:ring-orange-500 focus:border-orange-500"
                    }`}
                    placeholder="••••••••"
                    {...register("password")}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                    className="absolute inset-y-0 right-0 flex items-center pr-3 text-gray-400 hover:text-gray-600 transition focus:outline-none"
                  >
                    {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
                {errors.password && (
                  <p id="password-error" className="mt-1.5 text-xs text-red-600" role="alert">
                    {errors.password.message}
                  </p>
                )}
              </div>

              {/* Submit Button */}
              <button
                type="submit"
                disabled={isSubmitting}
                className="w-full flex items-center justify-center py-3 px-4 rounded-xl text-white font-medium bg-orange-600 hover:bg-orange-700 disabled:opacity-60 disabled:cursor-not-allowed transition shadow-md hover:shadow-lg focus:outline-none focus:ring-2 focus:ring-orange-500 focus:ring-offset-2"
              >
                {isSubmitting ? (
                  <>
                    <Loader2 className="w-5 h-5 mr-2 animate-spin" aria-hidden="true" />
                    Đang đăng nhập...
                  </>
                ) : (
                  "Đăng nhập"
                )}
              </button>
            </form>

            {/* Divider */}
            <div className="relative flex items-center justify-center">
              <div className="border-t border-gray-200 w-full" />
              <span className="bg-white px-3 text-xs uppercase tracking-wider text-gray-400 font-medium absolute">
                Hoặc
              </span>
            </div>

            {/* Google Login Section - GIS Integration */}
            {googleClientId ? (
              <div className="flex flex-col items-center justify-center space-y-2">
                <div id="googleSignInDiv" className="w-full flex justify-center min-h-[44px]" />
                {isGoogleSubmitting && (
                  <div className="flex items-center gap-2 text-sm text-gray-500">
                    <Loader2 className="w-4 h-4 animate-spin" />
                    <span>Đang xác thực Google...</span>
                  </div>
                )}
              </div>
            ) : (
              <div className="space-y-1.5">
                <button
                  type="button"
                  disabled
                  aria-describedby="google-oauth-note"
                  className="w-full flex items-center justify-center py-2.5 px-4 rounded-xl border border-gray-300 bg-gray-50 text-gray-400 font-medium text-sm cursor-not-allowed opacity-75 shadow-sm"
                >
                  <svg className="w-4 h-4 mr-2.5 opacity-50" viewBox="0 0 24 24" aria-hidden="true">
                    <path
                      fill="#4285F4"
                      d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
                    />
                    <path
                      fill="#34A853"
                      d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
                    />
                    <path
                      fill="#FBBC05"
                      d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.06H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.94l2.85-2.22.81-.63z"
                    />
                    <path
                      fill="#EA4335"
                      d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.06l3.66 2.84c.87-2.6 3.3-4.52 6.16-4.52z"
                    />
                  </svg>
                  Đăng nhập với Google
                </button>
                <p id="google-oauth-note" className="text-xs text-amber-700 text-center bg-amber-50 rounded-lg py-1 px-2 border border-amber-200">
                  Google login chưa được kích hoạt (cần cấu hình NEXT_PUBLIC_GOOGLE_CLIENT_ID)
                </p>
              </div>
            )}

            {/* Link to Register */}
            <div className="text-center pt-2">
              <p className="text-sm text-gray-600">
                Chưa có tài khoản?{" "}
                <Link
                  href="/auth/register"
                  className="font-medium text-orange-600 hover:text-orange-700 underline-offset-4 hover:underline transition focus:outline-none focus:ring-2 focus:ring-orange-500 rounded"
                >
                  Đăng ký
                </Link>
              </p>
            </div>
          </div>
        )}
        </div>
      </div>
    </>
  );
}
