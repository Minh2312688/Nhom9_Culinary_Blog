"use client";

import React, { useState, Suspense } from "react";
import Link from "next/link";
import { useSearchParams, useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { resetPasswordApi } from "@/lib/auth-api";
import { ApiError } from "@/lib/api";
import { Eye, EyeOff, Loader2, AlertCircle, CheckCircle2, Lock, ArrowLeft, KeyRound } from "lucide-react";
import { BrandLink } from "../../site-ui";

const resetPasswordSchema = z
  .object({
    email: z.string().email("Địa chỉ email không hợp lệ."),
    token: z.string().min(1, "Vui lòng nhập mã xác nhận đặt lại mật khẩu."),
    newPassword: z
      .string()
      .min(8, "Mật khẩu phải có ít nhất 8 ký tự.")
      .regex(/[A-Z]/, "Mật khẩu phải chứa ít nhất 1 chữ in hoa.")
      .regex(/[a-z]/, "Mật khẩu phải chứa ít nhất 1 chữ thường.")
      .regex(/[0-9]/, "Mật khẩu phải chứa ít nhất 1 chữ số.")
      .regex(/[^a-zA-Z0-9]/, "Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt."),
    confirmPassword: z.string().min(1, "Vui lòng xác nhận mật khẩu mới."),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: "Mật khẩu xác nhận không khớp.",
    path: ["confirmPassword"],
  });

type ResetPasswordFormData = z.infer<typeof resetPasswordSchema>;

function ResetPasswordForm() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const [showPassword, setShowPassword] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const initialEmail = searchParams.get("email") || "";
  const initialToken = searchParams.get("token") || "";

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordFormData>({
    resolver: zodResolver(resetPasswordSchema),
    mode: "onBlur",
    defaultValues: {
      email: initialEmail,
      token: initialToken,
      newPassword: "",
      confirmPassword: "",
    },
  });

  const onSubmit = async (data: ResetPasswordFormData) => {
    setServerError(null);
    try {
      await resetPasswordApi({
        email: data.email,
        token: data.token,
        newPassword: data.newPassword,
        confirmPassword: data.confirmPassword,
      });
      setSuccess(true);
    } catch (err) {
      const apiErr = err as ApiError;
      setServerError(
        apiErr.detail || "Mã xác thực không hợp lệ hoặc đã hết hạn. Vui lòng thử lại."
      );
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-4 bg-gradient-to-br from-amber-50/60 via-white to-orange-50/40">
      <div className="w-full max-w-md bg-white rounded-2xl shadow-xl border border-gray-100 p-8">
        <div className="mb-6">
          <div className="mb-4">
            <BrandLink />
          </div>
          <div className="w-12 h-12 bg-orange-100 text-orange-600 rounded-xl flex items-center justify-center mb-3">
            <KeyRound className="w-6 h-6" />
          </div>
          <h1 className="text-2xl font-bold text-gray-900 tracking-tight">
            Đặt lại mật khẩu
          </h1>
          <p className="text-sm text-gray-500 mt-1">
            Nhập mã xác thực và mật khẩu mới cho tài khoản của bạn.
          </p>
        </div>

        {success ? (
          <div className="space-y-6">
            <div
              className="flex items-start gap-3 rounded-xl bg-green-50 border border-green-200 p-4 text-sm text-green-800"
              role="status"
            >
              <CheckCircle2 className="w-5 h-5 text-green-600 shrink-0 mt-0.5" />
              <div>
                <p className="font-semibold">Đặt lại mật khẩu thành công!</p>
                <p className="mt-1 text-green-700">
                  Mật khẩu mới của bạn đã được cập nhật. Bạn có thể sử dụng mật khẩu này để đăng nhập ngay bây giờ.
                </p>
              </div>
            </div>

            <Link
              href="/auth/login"
              className="w-full flex items-center justify-center py-3 px-4 rounded-xl text-white font-medium bg-orange-600 hover:bg-orange-700 transition shadow-sm text-sm"
            >
              Đăng nhập ngay
            </Link>
          </div>
        ) : (
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            {serverError && (
              <div
                className="flex items-start gap-3 rounded-xl bg-red-50 border border-red-200 p-4 text-sm text-red-800"
                role="alert"
              >
                <AlertCircle className="w-5 h-5 text-red-600 shrink-0 mt-0.5" />
                <div className="flex-1">{serverError}</div>
              </div>
            )}

            {/* Email */}
            <div>
              <label
                htmlFor="email"
                className="block text-sm font-medium text-gray-700 mb-1"
              >
                Địa chỉ Email <span className="text-red-500">*</span>
              </label>
              <input
                id="email"
                type="email"
                autoComplete="email"
                disabled={isSubmitting}
                className={`w-full px-4 py-2 rounded-xl border bg-gray-50/50 text-gray-900 text-sm transition focus:bg-white focus:outline-none focus:ring-2 ${
                  errors.email
                    ? "border-red-400 focus:ring-red-500"
                    : "border-gray-200 focus:ring-orange-500 focus:border-orange-500"
                }`}
                placeholder="chef@example.com"
                {...register("email")}
              />
              {errors.email && (
                <p className="mt-1 text-xs text-red-600">{errors.email.message}</p>
              )}
            </div>

            {/* Token */}
            <div>
              <label
                htmlFor="token"
                className="block text-sm font-medium text-gray-700 mb-1"
              >
                Mã xác thực (Token) <span className="text-red-500">*</span>
              </label>
              <input
                id="token"
                type="text"
                disabled={isSubmitting}
                className={`w-full px-4 py-2 rounded-xl border bg-gray-50/50 text-gray-900 text-sm font-mono transition focus:bg-white focus:outline-none focus:ring-2 ${
                  errors.token
                    ? "border-red-400 focus:ring-red-500"
                    : "border-gray-200 focus:ring-orange-500 focus:border-orange-500"
                }`}
                placeholder="Nhập mã xác thực từ email..."
                {...register("token")}
              />
              {errors.token && (
                <p className="mt-1 text-xs text-red-600">{errors.token.message}</p>
              )}
            </div>

            {/* New Password */}
            <div>
              <label
                htmlFor="newPassword"
                className="block text-sm font-medium text-gray-700 mb-1"
              >
                Mật khẩu mới <span className="text-red-500">*</span>
              </label>
              <div className="relative">
                <input
                  id="newPassword"
                  type={showPassword ? "text" : "password"}
                  disabled={isSubmitting}
                  className={`w-full px-4 py-2 pr-11 rounded-xl border bg-gray-50/50 text-gray-900 text-sm transition focus:bg-white focus:outline-none focus:ring-2 ${
                    errors.newPassword
                      ? "border-red-400 focus:ring-red-500"
                      : "border-gray-200 focus:ring-orange-500 focus:border-orange-500"
                  }`}
                  placeholder="Tối thiểu 8 ký tự..."
                  {...register("newPassword")}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                  className="absolute inset-y-0 right-0 flex items-center pr-3 text-gray-400 hover:text-gray-600"
                >
                  {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
              {errors.newPassword && (
                <p className="mt-1 text-xs text-red-600">{errors.newPassword.message}</p>
              )}
            </div>

            {/* Confirm Password */}
            <div>
              <label
                htmlFor="confirmPassword"
                className="block text-sm font-medium text-gray-700 mb-1"
              >
                Xác nhận mật khẩu mới <span className="text-red-500">*</span>
              </label>
              <input
                id="confirmPassword"
                type={showPassword ? "text" : "password"}
                disabled={isSubmitting}
                className={`w-full px-4 py-2 rounded-xl border bg-gray-50/50 text-gray-900 text-sm transition focus:bg-white focus:outline-none focus:ring-2 ${
                  errors.confirmPassword
                    ? "border-red-400 focus:ring-red-500"
                    : "border-gray-200 focus:ring-orange-500 focus:border-orange-500"
                }`}
                placeholder="Nhập lại mật khẩu mới..."
                {...register("confirmPassword")}
              />
              {errors.confirmPassword && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.confirmPassword.message}
                </p>
              )}
            </div>

            {/* Submit Button */}
            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full flex items-center justify-center py-3 px-4 rounded-xl text-white font-medium bg-orange-600 hover:bg-orange-700 disabled:opacity-60 disabled:cursor-not-allowed transition shadow-md hover:shadow-lg focus:outline-none focus:ring-2 focus:ring-orange-500 mt-2"
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="w-5 h-5 mr-2 animate-spin" />
                  Đang xử lý...
                </>
              ) : (
                "Cập nhật mật khẩu"
              )}
            </button>

            {/* Back to Login */}
            <div className="text-center pt-2">
              <Link
                href="/auth/login"
                className="inline-flex items-center text-sm font-medium text-gray-600 hover:text-gray-900 transition"
              >
                <ArrowLeft className="w-4 h-4 mr-1.5" />
                Quay lại đăng nhập
              </Link>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}

export default function ResetPasswordPage() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen flex items-center justify-center">
          <Loader2 className="w-8 h-8 text-orange-600 animate-spin" />
        </div>
      }
    >
      <ResetPasswordForm />
    </Suspense>
  );
}
