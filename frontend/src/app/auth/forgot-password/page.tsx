"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { forgotPasswordApi } from "@/lib/auth-api";
import { ApiError } from "@/lib/api";
import { Mail, Loader2, AlertCircle, CheckCircle2, ArrowLeft, KeyRound, RotateCcw, Inbox } from "lucide-react";
import { BrandLink } from "../../site-ui";

const forgotPasswordSchema = z.object({
  email: z.string().email("Địa chỉ email không hợp lệ."),
});

type ForgotPasswordFormData = z.infer<typeof forgotPasswordSchema>;

export default function ForgotPasswordPage() {
  const [serverError, setServerError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [submittedEmail, setSubmittedEmail] = useState<string>("");

  const {
    register,
    handleSubmit,
    reset,
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
      setSubmittedEmail(data.email);
      setSuccessMessage(
        response.message ||
          "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi đến hộp thư của bạn."
      );
    } catch (err) {
      const apiErr = err as ApiError;
      setServerError(apiErr.detail || "Không thể gửi yêu cầu đặt lại mật khẩu. Vui lòng thử lại sau.");
    }
  };

  const handleResetForm = () => {
    setSuccessMessage(null);
    setServerError(null);
    reset();
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
            Nhập email tài khoản của bạn để nhận liên kết xác thực đặt lại mật khẩu.
          </p>
        </div>

        {/* Success Alert & Instructions */}
        {successMessage ? (
          <div className="space-y-5">
            <div
              className="flex items-start gap-3 rounded-xl bg-green-50 border border-green-200 p-4 text-sm text-green-900"
              role="status"
            >
              <CheckCircle2 className="w-5 h-5 text-green-600 shrink-0 mt-0.5" />
              <div>
                <p className="font-semibold text-green-950">Đã tiếp nhận yêu cầu đặt lại mật khẩu</p>
                <p className="mt-1 text-green-800 leading-relaxed text-xs sm:text-sm">
                  {successMessage}
                </p>
              </div>
            </div>

            {/* Clear instructions on next steps */}
            <div className="rounded-xl bg-orange-50/70 border border-orange-200/80 p-4 text-xs text-orange-950 space-y-2">
              <div className="flex items-center gap-2 font-semibold text-orange-900">
                <Inbox className="w-4 h-4 text-orange-600 shrink-0" />
                <span>Các bước tiếp theo:</span>
              </div>
              <ol className="list-decimal list-inside space-y-1 text-orange-900/90 pl-1 leading-relaxed">
                <li>Mở hộp thư email của bạn (kiểm tra cả thư mục <strong>Hộp thư đến</strong> và <strong>Spam / Rác</strong>).</li>
                <li>Nhấn vào nút <strong>&quot;Đặt lại mật khẩu&quot;</strong> trong email nhận được.</li>
                <li>Hệ thống sẽ tự động xác thực token và mở trang để bạn nhập mật khẩu mới.</li>
              </ol>
            </div>

            <div className="flex flex-col gap-2.5 pt-1">
              <button
                type="button"
                onClick={handleResetForm}
                className="w-full flex items-center justify-center py-2.5 px-4 rounded-xl text-gray-700 font-medium border border-gray-300 hover:bg-gray-50 transition text-sm"
              >
                <RotateCcw className="w-4 h-4 mr-1.5" />
                Gửi lại yêu cầu với email khác
              </button>
              <Link
                href="/auth/login"
                className="w-full flex items-center justify-center py-2.5 px-4 rounded-xl text-orange-600 hover:text-orange-700 font-medium text-sm transition"
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

            {/* Email Field */}
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
                  className={`w-full px-4 py-2.5 pl-11 rounded-xl border bg-gray-50/50 text-gray-900 text-sm transition focus:bg-white focus:outline-none focus:ring-2 ${
                    errors.email
                      ? "border-red-400 focus:ring-red-500"
                      : "border-gray-200 focus:ring-orange-500 focus:border-orange-500"
                  }`}
                  placeholder="chef@example.com"
                  {...register("email")}
                />
                <Mail className="w-5 h-5 text-gray-400 absolute left-3.5 top-3 pointer-events-none" />
              </div>
              {errors.email && (
                <p className="mt-1 text-xs text-red-600">{errors.email.message}</p>
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
                  Đang gửi yêu cầu...
                </>
              ) : (
                "Gửi liên kết đặt lại mật khẩu"
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
