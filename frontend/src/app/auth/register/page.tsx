"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { registerApi, ApiError } from "@/lib/api";
import { Eye, EyeOff, Loader2, CheckCircle2, AlertCircle, ChefHat } from "lucide-react";

// Password rule per NFR-SEC-001: >=8 chars, >=1 uppercase, >=1 lowercase, >=1 digit, >=1 special char
const passwordRegex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{8,}$/;

const registerSchema = z
  .object({
    displayName: z
      .string()
      .min(2, "Tên hiển thị phải có ít nhất 2 ký tự.")
      .max(100, "Tên hiển thị không được vượt quá 100 ký tự."),
    email: z.string().email("Địa chỉ email không hợp lệ."),
    password: z
      .string()
      .regex(
        passwordRegex,
        "Mật khẩu phải từ 8 ký tự, bao gồm ít nhất 1 chữ hoa, 1 chữ thường, 1 chữ số và 1 ký tự đặc biệt."
      ),
    confirmPassword: z.string().min(1, "Vui lòng xác nhận lại mật khẩu."),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Mật khẩu xác nhận không khớp.",
    path: ["confirmPassword"],
  });

type RegisterFormData = z.infer<typeof registerSchema>;

export default function RegisterPage() {
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormData>({
    resolver: zodResolver(registerSchema),
    mode: "onBlur",
  });

  const onSubmit = async (data: RegisterFormData) => {
    setServerError(null);
    try {
      // NOTE: confirmPassword is UI CONVENIENCE ONLY. Not sent to backend.
      await registerApi({
        email: data.email,
        password: data.password,
        displayName: data.displayName,
      });
      setSuccess(true);
    } catch (err) {
      const apiErr = err as ApiError;
      setServerError(apiErr.detail || "Đã xảy ra lỗi trong quá trình đăng ký.");
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-4 sm:p-6 lg:p-8">
      <div className="w-full max-w-md bg-white rounded-2xl shadow-xl border border-gray-100 p-6 sm:p-8">
        {/* Header */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-14 h-14 rounded-2xl bg-orange-100 text-orange-600 mb-4 shadow-sm">
            <ChefHat className="w-8 h-8" aria-hidden="true" />
          </div>
          <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-gray-900">
            Tạo tài khoản mới
          </h1>
          <p className="text-sm text-gray-500 mt-2">
            Gia nhập cộng đồng Culinary Blog và chia sẻ niềm đam mê ẩm thực
          </p>
        </div>

        {/* Success Banner */}
        {success ? (
          <div
            className="rounded-xl bg-green-50 border border-green-200 p-6 text-center space-y-4"
            role="status"
          >
            <CheckCircle2 className="w-12 h-12 text-green-600 mx-auto" />
            <h2 className="text-lg font-semibold text-green-900">Đăng ký thành công!</h2>
            <p className="text-sm text-green-700">
              Tài khoản tác giả của bạn đã được tạo. Bạn có thể đăng nhập ngay bây giờ.
            </p>
            <div className="pt-2">
              <Link
                href="/auth/login"
                className="inline-flex items-center justify-center w-full px-4 py-2.5 bg-orange-600 hover:bg-orange-700 text-white font-medium rounded-xl transition shadow-md hover:shadow-lg focus:outline-none focus:ring-2 focus:ring-orange-500 focus:ring-offset-2"
              >
                Đăng nhập ngay
              </Link>
            </div>
          </div>
        ) : (
          /* Register Form */
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" noValidate>
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

            {/* Display Name */}
            <div>
              <label
                htmlFor="displayName"
                className="block text-sm font-medium text-gray-700 mb-1"
              >
                Tên hiển thị <span className="text-red-500">*</span>
              </label>
              <input
                id="displayName"
                type="text"
                autoComplete="name"
                disabled={isSubmitting}
                aria-invalid={errors.displayName ? "true" : "false"}
                aria-describedby={errors.displayName ? "displayName-error" : undefined}
                className={`w-full px-4 py-2.5 rounded-xl border bg-gray-50/50 text-gray-900 text-sm transition focus:bg-white focus:outline-none focus:ring-2 ${
                  errors.displayName
                    ? "border-red-400 focus:ring-red-500"
                    : "border-gray-200 focus:ring-orange-500 focus:border-orange-500"
                }`}
                placeholder="Nguyễn Văn A"
                {...register("displayName")}
              />
              {errors.displayName && (
                <p id="displayName-error" className="mt-1.5 text-xs text-red-600" role="alert">
                  {errors.displayName.message}
                </p>
              )}
            </div>

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
                  autoComplete="new-password"
                  disabled={isSubmitting}
                  aria-invalid={errors.password ? "true" : "false"}
                  aria-describedby={errors.password ? "password-error" : "password-hint"}
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
              <p id="password-hint" className="mt-1 text-xs text-gray-400">
                Ít nhất 8 ký tự, 1 hoa, 1 thường, 1 số, 1 ký tự đặc biệt.
              </p>
              {errors.password && (
                <p id="password-error" className="mt-1 text-xs text-red-600" role="alert">
                  {errors.password.message}
                </p>
              )}
            </div>

            {/* Confirm Password (UI convenience only) */}
            <div>
              <label
                htmlFor="confirmPassword"
                className="block text-sm font-medium text-gray-700 mb-1"
              >
                Xác nhận mật khẩu <span className="text-red-500">*</span>
              </label>
              <div className="relative">
                <input
                  id="confirmPassword"
                  type={showConfirmPassword ? "text" : "password"}
                  autoComplete="new-password"
                  disabled={isSubmitting}
                  aria-invalid={errors.confirmPassword ? "true" : "false"}
                  aria-describedby={
                    errors.confirmPassword ? "confirmPassword-error" : undefined
                  }
                  className={`w-full px-4 py-2.5 pr-11 rounded-xl border bg-gray-50/50 text-gray-900 text-sm transition focus:bg-white focus:outline-none focus:ring-2 ${
                    errors.confirmPassword
                      ? "border-red-400 focus:ring-red-500"
                      : "border-gray-200 focus:ring-orange-500 focus:border-orange-500"
                  }`}
                  placeholder="••••••••"
                  {...register("confirmPassword")}
                />
                <button
                  type="button"
                  onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                  aria-label={showConfirmPassword ? "Ẩn xác nhận mật khẩu" : "Hiện xác nhận mật khẩu"}
                  className="absolute inset-y-0 right-0 flex items-center pr-3 text-gray-400 hover:text-gray-600 transition focus:outline-none"
                >
                  {showConfirmPassword ? (
                    <EyeOff className="w-4 h-4" />
                  ) : (
                    <Eye className="w-4 h-4" />
                  )}
                </button>
              </div>
              {errors.confirmPassword && (
                <p
                  id="confirmPassword-error"
                  className="mt-1.5 text-xs text-red-600"
                  role="alert"
                >
                  {errors.confirmPassword.message}
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
                  Đang xử lý đăng ký...
                </>
              ) : (
                "Đăng ký"
              )}
            </button>

            {/* Link to Login */}
            <div className="text-center pt-2">
              <p className="text-sm text-gray-600">
                Đã có tài khoản?{" "}
                <Link
                  href="/auth/login"
                  className="font-medium text-orange-600 hover:text-orange-700 underline-offset-4 hover:underline transition focus:outline-none focus:ring-2 focus:ring-orange-500 rounded"
                >
                  Đăng nhập
                </Link>
              </p>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
