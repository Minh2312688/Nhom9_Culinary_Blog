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
  updateProfileApi,
  UpdateProfileRequest,
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
  Pencil,
  Save,
  X,
} from "lucide-react";

export default function ProfilePage() {
  const router = useRouter();
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isLoggingOut, setIsLoggingOut] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);

  // Edit Mode States (FR-AUTH-007)
  const [isEditing, setIsEditing] = useState(false);
  const [editDisplayName, setEditDisplayName] = useState("");
  const [editAvatarUrl, setEditAvatarUrl] = useState("");
  const [editBio, setEditBio] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const [editError, setEditError] = useState<string | null>(null);
  const [editSuccess, setEditSuccess] = useState<string | null>(null);

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

          const refreshedProfile = await getProfileApi(newAuth.accessToken);
          setProfile(refreshedProfile);
          setLoading(false);
          return;
        } catch {
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

  const handleStartEdit = () => {
    if (!profile) return;
    setEditDisplayName(profile.displayName || "");
    setEditAvatarUrl(profile.avatarUrl || "");
    setEditBio(profile.bio || "");
    setEditError(null);
    setEditSuccess(null);
    setIsEditing(true);
  };

  const handleCancelEdit = () => {
    setIsEditing(false);
    setEditError(null);
  };

  const handleSaveProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSaving) return;
    setEditError(null);
    setEditSuccess(null);

    const trimmedName = editDisplayName.trim();
    if (!trimmedName) {
      setEditError("Tên hiển thị không được để trống.");
      return;
    }
    if (trimmedName.length < 2 || trimmedName.length > 100) {
      setEditError("Tên hiển thị phải từ 2 đến 100 ký tự.");
      return;
    }

    const trimmedAvatar = editAvatarUrl.trim();
    if (trimmedAvatar) {
      try {
        const parsedUrl = new URL(trimmedAvatar);
        if (parsedUrl.protocol !== "http:" && parsedUrl.protocol !== "https:") {
          setEditError("URL ảnh đại diện phải có giao thức http:// hoặc https://");
          return;
        }
      } catch {
        setEditError("URL ảnh đại diện không hợp lệ.");
        return;
      }
    }

    const payload: UpdateProfileRequest = {
      displayName: trimmedName,
      avatarUrl: trimmedAvatar || null,
      bio: editBio.trim() || null,
    };

    setIsSaving(true);
    let token = sessionStorage.getItem("culinary_access_token") || "";
    const refreshToken = sessionStorage.getItem("culinary_refresh_token") || "";

    try {
      try {
        const updated = await updateProfileApi(token, payload);
        setProfile(updated);
        setIsEditing(false);
        setEditSuccess("Cập nhật hồ sơ thành công!");
        setIsSaving(false);
        return;
      } catch (err) {
        const apiErr = err as ApiError;
        if (apiErr.status === 401 && refreshToken) {
          const newAuth = await refreshTokenApi(refreshToken);
          sessionStorage.setItem("culinary_access_token", newAuth.accessToken);
          sessionStorage.setItem("culinary_refresh_token", newAuth.refreshToken);
          token = newAuth.accessToken;

          const updated = await updateProfileApi(token, payload);
          setProfile(updated);
          setIsEditing(false);
          setEditSuccess("Cập nhật hồ sơ thành công!");
          setIsSaving(false);
          return;
        }
        throw apiErr;
      }
    } catch (err) {
      const apiErr = err as ApiError;
      if (apiErr.errors) {
        const detailList = Object.values(apiErr.errors).flat().join(" ");
        setEditError(detailList || apiErr.detail || "Dữ liệu không hợp lệ.");
      } else {
        setEditError(apiErr.detail || "Không thể cập nhật hồ sơ. Vui lòng thử lại.");
      }
      setIsSaving(false);
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
          {/* Avatar and Top Actions */}
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

            {/* Action Buttons: Edit & Logout */}
            {!loading && (
              <div className="flex items-center gap-2">
                {!isEditing && (
                  <button
                    type="button"
                    id="edit-profile-button"
                    onClick={handleStartEdit}
                    aria-label="Chỉnh sửa thông tin hồ sơ"
                    className="inline-flex items-center justify-center px-3.5 py-2.5 rounded-xl border border-orange-200 bg-orange-50 text-orange-700 hover:bg-orange-100 hover:border-orange-300 font-medium text-sm transition focus:outline-none focus:ring-2 focus:ring-orange-400 shadow-sm"
                  >
                    <Pencil className="w-4 h-4 mr-1.5" aria-hidden="true" />
                    Chỉnh sửa
                  </button>
                )}

                <button
                  type="button"
                  id="logout-button"
                  onClick={handleLogout}
                  disabled={isLoggingOut}
                  aria-label="Đăng xuất khỏi hệ thống"
                  className="inline-flex items-center justify-center px-3.5 py-2.5 rounded-xl border border-red-200 text-red-600 hover:bg-red-50 hover:border-red-300 font-medium text-sm transition focus:outline-none focus:ring-2 focus:ring-red-400 disabled:opacity-60 disabled:cursor-not-allowed shadow-sm"
                >
                  {isLoggingOut ? (
                    <>
                      <Loader2 className="w-4 h-4 mr-2 animate-spin" aria-hidden="true" />
                      Đang đăng xuất...
                    </>
                  ) : (
                    <>
                      <LogOut className="w-4 h-4 mr-1.5" aria-hidden="true" />
                      Đăng xuất
                    </>
                  )}
                </button>
              </div>
            )}
          </div>

          {/* Success Feedback Alert */}
          {editSuccess && (
            <div
              className="mb-6 flex items-start gap-3 rounded-xl bg-green-50 border border-green-200 p-4 text-sm text-green-800"
              role="status"
            >
              <CheckCircle2 className="w-5 h-5 text-green-600 shrink-0 mt-0.5" aria-hidden="true" />
              <div className="flex-1 font-medium">{editSuccess}</div>
            </div>
          )}

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
          ) : profile && isEditing ? (
            /* EDIT PROFILE FORM (FR-AUTH-007) */
            <form onSubmit={handleSaveProfile} className="space-y-5">
              <div className="border-b border-gray-100 pb-3">
                <h2 className="text-lg font-bold text-gray-900">
                  Chỉnh sửa hồ sơ cá nhân
                </h2>
                <p className="text-xs text-gray-500 mt-0.5">
                  Cập nhật các thông tin hiển thị của bạn trên Culinary Blog.
                </p>
              </div>

              {/* Edit Error Alert */}
              {editError && (
                <div
                  className="flex items-start gap-3 rounded-xl bg-red-50 border border-red-200 p-3.5 text-sm text-red-800"
                  role="alert"
                >
                  <AlertCircle className="w-5 h-5 text-red-600 shrink-0 mt-0.5" aria-hidden="true" />
                  <div className="flex-1">{editError}</div>
                </div>
              )}

              {/* Display Name Input */}
              <div className="space-y-1.5">
                <label
                  htmlFor="edit-display-name"
                  className="block text-xs font-semibold text-gray-700 uppercase tracking-wider"
                >
                  Tên hiển thị <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  id="edit-display-name"
                  name="displayName"
                  value={editDisplayName}
                  onChange={(e) => setEditDisplayName(e.target.value)}
                  placeholder="Nhập tên hiển thị..."
                  maxLength={100}
                  required
                  className="w-full px-3.5 py-2.5 rounded-xl border border-gray-300 text-gray-900 text-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500 transition shadow-sm"
                />
              </div>

              {/* Avatar URL Input */}
              <div className="space-y-1.5">
                <label
                  htmlFor="edit-avatar-url"
                  className="block text-xs font-semibold text-gray-700 uppercase tracking-wider"
                >
                  URL Ảnh đại diện
                </label>
                <input
                  type="url"
                  id="edit-avatar-url"
                  name="avatarUrl"
                  value={editAvatarUrl}
                  onChange={(e) => setEditAvatarUrl(e.target.value)}
                  placeholder="https://example.com/avatar.jpg"
                  className="w-full px-3.5 py-2.5 rounded-xl border border-gray-300 text-gray-900 text-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500 transition shadow-sm"
                />
                <p className="text-xs text-gray-400">
                  Đường dẫn ảnh định dạng HTTP/HTTPS hợp lệ.
                </p>
              </div>

              {/* Bio Input */}
              <div className="space-y-1.5">
                <label
                  htmlFor="edit-bio"
                  className="block text-xs font-semibold text-gray-700 uppercase tracking-wider"
                >
                  Tiểu sử (Bio)
                </label>
                <textarea
                  id="edit-bio"
                  name="bio"
                  rows={3}
                  value={editBio}
                  onChange={(e) => setEditBio(e.target.value)}
                  placeholder="Mô tả đôi nét về phong cách ẩm thực của bạn..."
                  maxLength={500}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-gray-300 text-gray-900 text-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500 transition shadow-sm resize-none"
                />
                <div className="text-right text-xs text-gray-400">
                  {editBio.length} / 500 ký tự
                </div>
              </div>

              {/* Readonly Email Notice */}
              <div className="rounded-xl bg-gray-50 border border-gray-200/80 p-3 text-xs text-gray-500 flex items-center justify-between">
                <span>Email tài khoản: <strong>{profile.email}</strong></span>
                <span className="italic text-gray-400">Không thể thay đổi</span>
              </div>

              {/* Form Buttons */}
              <div className="flex items-center justify-end gap-3 pt-2">
                <button
                  type="button"
                  id="cancel-edit-button"
                  onClick={handleCancelEdit}
                  disabled={isSaving}
                  className="px-4 py-2.5 rounded-xl border border-gray-300 text-gray-700 hover:bg-gray-50 font-medium text-sm transition focus:outline-none focus:ring-2 focus:ring-gray-300 disabled:opacity-60"
                >
                  <X className="w-4 h-4 mr-1.5 inline" aria-hidden="true" />
                  Hủy
                </button>

                <button
                  type="submit"
                  id="save-profile-button"
                  disabled={isSaving}
                  className="inline-flex items-center justify-center px-5 py-2.5 rounded-xl bg-orange-600 hover:bg-orange-700 text-white font-medium text-sm transition focus:outline-none focus:ring-2 focus:ring-orange-500 disabled:opacity-60 shadow-sm"
                >
                  {isSaving ? (
                    <>
                      <Loader2 className="w-4 h-4 mr-2 animate-spin" aria-hidden="true" />
                      Đang lưu...
                    </>
                  ) : (
                    <>
                      <Save className="w-4 h-4 mr-1.5" aria-hidden="true" />
                      Lưu thay đổi
                    </>
                  )}
                </button>
              </div>
            </form>
          ) : profile ? (
            /* VIEW PROFILE CONTENT */
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
