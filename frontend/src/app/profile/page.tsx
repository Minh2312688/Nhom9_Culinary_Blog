"use client";

import React, { useState, useEffect, useCallback } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { ApiError } from "@/lib/api";
import {
  UserProfile,
  getProfileApi,
  refreshTokenApi,
  logoutApi,
} from "@/lib/auth-api";
import {
  ChefHat,
  LogOut,
  Mail,
  Calendar,
  Shield,
  Loader2,
  AlertCircle,
  RefreshCw,
  CheckCircle2,
  XCircle,
} from "lucide-react";

export default function ProfilePage() {
  const router = useRouter();
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isLoggingOut, setIsLoggingOut] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);

  const fetchProfileWithRetry = useCallback(async () => {
    setError(null);
    setLoading(true);

    if (typeof window === "undefined") return;

    const accessToken = sessionStorage.getItem("culinary_access_token");
    const refreshToken = sessionStorage.getItem("culinary_refresh_token");

    if (!accessToken && !refreshToken) {
      router.push("/auth/login");
      return;
    }

    try {
      if (accessToken) {
        try {
          const data = await getProfileApi(accessToken);
          setProfile(data);
          setLoading(false);
          return;
        } catch (err) {
          const apiErr = err as ApiError;
          // If not 401 Unauthorized, rethrow
          if (apiErr.status !== 401) {
            throw apiErr;
          }
        }
      }

      // 401 or no access token: attempt refresh token rotation ONCE
      if (refreshToken) {
        setIsRefreshing(true);
        try {
          const newAuth = await refreshTokenApi(refreshToken);
          sessionStorage.setItem("culinary_access_token", newAuth.accessToken);
          sessionStorage.setItem("culinary_refresh_token", newAuth.refreshToken);

          // Retry GET /me with new access token exactly once
          const refreshedProfile = await getProfileApi(newAuth.accessToken);
          setProfile(refreshedProfile);
          setLoading(false);
          return;
        } catch {
          // Refresh failed: clear auth state and redirect to login
          sessionStorage.removeItem("culinary_access_token");
          sessionStorage.removeItem("culinary_refresh_token");
          router.push("/auth/login");
          return;
        } finally {
          setIsRefreshing(false);
        }
      } else {
        sessionStorage.removeItem("culinary_access_token");
        sessionStorage.removeItem("culinary_refresh_token");
        router.push("/auth/login");
      }
    } catch (err) {
      const apiErr = err as ApiError;
      setError(apiErr.detail || "Không thể tải thông tin hồ sơ. Vui lòng thử lại.");
      setLoading(false);
    }
  }, [router]);

  useEffect(() => {
    fetchProfileWithRetry();
  }, [fetchProfileWithRetry]);

  const handleLogout = async () => {
    if (isLoggingOut) return;
    setIsLoggingOut(true);

    const accessToken = sessionStorage.getItem("culinary_access_token") || "";
    const refreshToken = sessionStorage.getItem("culinary_refresh_token") || "";

    try {
      if (accessToken && refreshToken) {
        await logoutApi(accessToken, refreshToken);
      }
    } catch {
      // Ignore network / logout errors: proceed to clear client session
    } finally {
      sessionStorage.removeItem("culinary_access_token");
      sessionStorage.removeItem("culinary_refresh_token");
      router.push("/auth/login");
    }
  };

  // Helper formatting for registration date
  const formatDate = (dateString?: string) => {
    if (!dateString) return "Không xác định";
    try {
      const date = new Date(dateString);
      return new Intl.DateTimeFormat("vi-VN", {
        year: "numeric",
        month: "long",
        day: "numeric",
      }).format(date);
    } catch {
      return dateString;
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-4 sm:p-6 lg:p-8 bg-gradient-to-br from-amber-50/50 via-white to-orange-50/40">
      <div className="w-full max-w-lg bg-white rounded-2xl shadow-xl border border-gray-100 overflow-hidden">
        {/* Decorative Top Banner */}
        <div className="h-28 bg-gradient-to-r from-orange-500 via-amber-500 to-orange-600 relative flex items-end justify-between px-6 pb-3">
          <span className="text-xs font-semibold text-white/80 uppercase tracking-widest">
            Hồ sơ đầu bếp
          </span>
          <div className="flex items-center space-x-2">
            <Link
              href="/"
              className="text-xs text-white/90 hover:text-white underline underline-offset-2 transition"
            >
              Trang chủ
            </Link>
          </div>
        </div>

        <div className="px-6 sm:px-8 pb-8 pt-0 relative">
          {/* Avatar and Header */}
          <div className="flex flex-col sm:flex-row items-center sm:items-end justify-between -mt-14 mb-6 gap-4">
            <div className="relative">
              {profile?.avatarUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={profile.avatarUrl}
                  alt={profile.displayName || "Avatar"}
                  className="w-24 h-24 rounded-2xl object-cover border-4 border-white shadow-md bg-white"
                />
              ) : (
                <div className="w-24 h-24 rounded-2xl bg-orange-100 border-4 border-white shadow-md flex items-center justify-center text-orange-600">
                  <ChefHat className="w-12 h-12" aria-hidden="true" />
                </div>
              )}
            </div>

            {/* Logout Action Button */}
            {!loading && (
              <button
                type="button"
                id="logout-button"
                onClick={handleLogout}
                disabled={isLoggingOut}
                aria-label="Đăng xuất khỏi hệ thống"
                className="inline-flex items-center justify-center px-4 py-2.5 rounded-xl border border-red-200 text-red-600 hover:bg-red-50 hover:border-red-300 font-medium text-sm transition focus:outline-none focus:ring-2 focus:ring-red-400 disabled:opacity-60 disabled:cursor-not-allowed shadow-sm"
              >
                {isLoggingOut ? (
                  <>
                    <Loader2 className="w-4 h-4 mr-2 animate-spin" aria-hidden="true" />
                    Đang đăng xuất...
                  </>
                ) : (
                  <>
                    <LogOut className="w-4 h-4 mr-2" aria-hidden="true" />
                    Đăng xuất
                  </>
                )}
              </button>
            )}
          </div>

          {/* Loading State */}
          {loading ? (
            <div className="py-12 flex flex-col items-center justify-center space-y-4" role="status">
              <Loader2 className="w-10 h-10 text-orange-600 animate-spin" />
              <p className="text-sm text-gray-500 font-medium">
                {isRefreshing ? "Đang làm mới phiên làm việc..." : "Đang tải thông tin hồ sơ..."}
              </p>
            </div>
          ) : error ? (
            /* Error State */
            <div className="space-y-6">
              <div
                className="flex items-start gap-3 rounded-xl bg-red-50 border border-red-200 p-4 text-sm text-red-800"
                role="alert"
              >
                <AlertCircle className="w-5 h-5 text-red-600 shrink-0 mt-0.5" aria-hidden="true" />
                <div className="flex-1">{error}</div>
              </div>

              <div className="flex justify-center">
                <button
                  type="button"
                  onClick={fetchProfileWithRetry}
                  className="inline-flex items-center px-4 py-2 rounded-xl text-white font-medium bg-orange-600 hover:bg-orange-700 transition shadow-sm text-sm"
                >
                  <RefreshCw className="w-4 h-4 mr-2" />
                  Thử lại
                </button>
              </div>
            </div>
          ) : profile ? (
            /* Profile Content (Safe Fields Only) */
            <div className="space-y-6">
              {/* Name & Roles */}
              <div>
                <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
                  <span>{profile.displayName}</span>
                </h1>
                {profile.bio && (
                  <p className="text-sm text-gray-600 mt-1 italic">
                    &ldquo;{profile.bio}&rdquo;
                  </p>
                )}
                {/* Roles Badges */}
                <div className="flex flex-wrap gap-2 mt-3">
                  {profile.roles && profile.roles.length > 0 ? (
                    profile.roles.map((role) => (
                      <span
                        key={role}
                        className="inline-flex items-center px-2.5 py-1 rounded-lg text-xs font-semibold bg-orange-50 text-orange-700 border border-orange-200"
                      >
                        <Shield className="w-3 h-3 mr-1" />
                        {role}
                      </span>
                    ))
                  ) : (
                    <span className="inline-flex items-center px-2.5 py-1 rounded-lg text-xs font-medium bg-gray-100 text-gray-700">
                      Thành viên
                    </span>
                  )}
                </div>
              </div>

              {/* Details Card */}
              <div className="rounded-xl border border-gray-100 bg-gray-50/60 p-4 space-y-3.5">
                {/* Email */}
                <div className="flex items-center justify-between text-sm">
                  <div className="flex items-center text-gray-500">
                    <Mail className="w-4 h-4 mr-2.5 text-gray-400" />
                    <span>Email</span>
                  </div>
                  <div className="flex items-center gap-2 font-medium text-gray-800">
                    <span>{profile.email}</span>
                    {profile.emailConfirmed ? (
                      <span title="Email đã xác thực" className="inline-flex items-center text-green-600">
                        <CheckCircle2 className="w-4 h-4" />
                      </span>
                    ) : (
                      <span title="Email chưa xác thực" className="inline-flex items-center text-amber-500">
                        <XCircle className="w-4 h-4" />
                      </span>
                    )}
                  </div>
                </div>

                {/* Member Since */}
                <div className="flex items-center justify-between text-sm">
                  <div className="flex items-center text-gray-500">
                    <Calendar className="w-4 h-4 mr-2.5 text-gray-400" />
                    <span>Tham gia từ</span>
                  </div>
                  <span className="font-medium text-gray-800">
                    {formatDate(profile.createdAt)}
                  </span>
                </div>
              </div>

              {/* Security Notice Note */}
              <div className="rounded-xl bg-orange-50/70 border border-orange-100 p-3.5 text-xs text-orange-800 flex items-start gap-2.5">
                <ChefHat className="w-4 h-4 text-orange-600 shrink-0 mt-0.5" />
                <p>
                  Phiên đăng nhập được bảo mật bởi JWT Access Token và Refresh Token Rotation tự động.
                </p>
              </div>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}
