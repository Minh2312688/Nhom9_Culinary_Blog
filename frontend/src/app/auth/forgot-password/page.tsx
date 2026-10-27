"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { forgotPasswordApi } from "@/lib/auth-api";
import { ApiError } from "@/lib/api";
import { Mail, Loader2, AlertCircle, CheckCircle2, ArrowLeft, KeyRound } from "lucide-react";
import { BrandLink } from "../../site-ui";

const forgotPasswordSchema = z.object({
  email: z.string().email("Địa chỉ email không hợp lệ."),
});

type ForgotPasswordFormData = z.infer<typeof forgotPasswordSchema>;

export default function ForgotPasswordPage() {
  const [serverError, setServerError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordFormData>({
    resolver: zodResolver(forgotPasswordSchema),
    mode: "onBlur",
  });

  const onSubmit = async (data: ForgotPasswordFormData) => {
    setServerError(null);
    setSuccessMessage(null);
    try {
      const response = await forgotPasswordApi({ email: data.email });
      setSuccessMessage(
        response.message ||
          "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi đến hộp thư của bạn."
      );
    } catch (err) {
      const apiErr = err as ApiError;
      setServerError(apiErr.detail || "Không thể gửi yêu cầu đặt lại mật khẩu. Vui lòng thử lại sau.");
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
            Quên mật khẩu?
          </h1>
          <p className="text-sm text-gray-500 mt-1">
            Nhập email tài khoản của bạn để nhận mã xác nhận đặt lại mật khẩu.
          </p>
        </div>

        {/* Success Alert */}
        {successMessage ? (
          <div className="space-y-6">
            <div
              className="flex items-start gap-3 rounded-xl bg-green-50 border border-green-200 p-4 text-sm text-green-800"
              role="status"
            >
              <CheckCircle2 className="w-5 h-5 text-green-600 shrink-0 mt-0.5" />
              <div>
                <p className="font-semibold">Đã gửi yêu cầu thành công!</p>
                <p className="mt-1 text-green-700">{successMessage}</p>
              </div>
            </div>

            <div className="flex flex-col gap-3">
              <Link
                href="/auth/reset-password"
                className="w-full flex items-center justify-center py-2.5 px-4 rounded-xl text-white font-medium bg-orange-600 hover:bg-orange-700 transition shadow-sm text-sm"
              >
                Nhập mã đặt lại mật khẩu
              </Link>
              <Link
                href="/auth/login"
                className="w-full flex items-center justify-center py-2.5 px-4 rounded-xl text-gray-700 font-medium border border-gray-300 hover:bg-gray-50 transition text-sm"
              >
                <ArrowLeft className="w-4 h-4 mr-1.5" />
                Quay lại đăng nhập
              </Link>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
            {/* Server Error Alert */}
            {serverError && (
              <div
                className="flex items-start gap-3 rounded-xl bg-red-50 border border-red-200 p-4 text-sm text-red-800"
                role="alert"
              >
                <AlertCircle className="w-5 h-5 text-red-600 shrink-0 mt-0.5" />
                <div className="flex-1">{serverError}</div>
              </div>
            )}

            {/* Email Input */}
            <div>
              <label
                htmlFor="email"
                className="block text-sm font-medium text-gray-700 mb-1"
              >
                Địa chỉ Email <span className="text-red-500">*</span>
              </label>
              <div className="relative">
                <input
                  id="email"
                  type="email"
                  autoComplete="email"
                  disabled={isSubmitting}
                  aria-invalid={errors.email ? "true" : "false"}
                  className={`w-full px-4 py-2.5 pl-10 rounded-xl border bg-gray-50/50 text-gray-900 text-sm transition focus:bg-white focus:outline-none focus:ring-2 ${
                    errors.email
                      ? "border-red-400 focus:ring-red-500"
                      : "border-gray-200 focus:ring-orange-500 focus:border-orange-500"
                  }`}
                  placeholder="chef@example.com"
                  {...register("email")}
                />
                <Mail className="w-4 h-4 text-gray-400 absolute left-3.5 top-3" />
              </div>
              {errors.email && (
                <p className="mt-1.5 text-xs text-red-600" role="alert">
                  {errors.email.message}
                </p>
              )}
            </div>

            {/* Submit Button */}
            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full flex items-center justify-center py-3 px-4 rounded-xl text-white font-medium bg-orange-600 hover:bg-orange-700 disabled:opacity-60 disabled:cursor-not-allowed transition shadow-md hover:shadow-lg focus:outline-none focus:ring-2 focus:ring-orange-500"
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="w-5 h-5 mr-2 animate-spin" />
                  Đang xử lý...
                </>
              ) : (
                "Gửi mã đặt lại mật khẩu"
              )}
            </button>

            {/* Back Link */}
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
