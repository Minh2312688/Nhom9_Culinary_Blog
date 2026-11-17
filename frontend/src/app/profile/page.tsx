"use client";

import React, { useEffect, useState } from "react";
import { signOut, useSession } from "next-auth/react";
import type { ApiError } from "@/lib/api";
import type { UpdateProfileRequest } from "@/lib/auth-api";
import { useChangePassword, useProfile, useUpdateProfile } from "@/lib/queries/auth";
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
  KeyRound,
  Lock,
  ArrowLeft,
} from "lucide-react";
import { BrandLink } from "../site-ui";

export default function ProfilePage() {
  const { data: session, status: sessionStatus } = useSession();
  const userId = session?.user?.id ?? "";
  const profileQuery = useProfile(userId, sessionStatus === "authenticated");
  const profile = profileQuery.data ?? null;
  const loading = sessionStatus === "loading" || (Boolean(userId) && profileQuery.isPending);
  const error = profileQuery.isError
    ? profileQuery.error?.detail || "Không thể tải thông tin hồ sơ. Vui lòng thử lại."
    : sessionStatus === "unauthenticated" ? "Vui lòng đăng nhập để xem hồ sơ." : null;
  const updateProfileMutation = useUpdateProfile(userId);
  const changePasswordMutation = useChangePassword();
  const isSaving = updateProfileMutation.isPending;
  const isSavingPassword = changePasswordMutation.isPending;
  const [isLoggingOut, setIsLoggingOut] = useState(false);

  // Edit Mode States (FR-AUTH-007)
  const [isEditing, setIsEditing] = useState(false);
  const [editDisplayName, setEditDisplayName] = useState("");
  const [editAvatarUrl, setEditAvatarUrl] = useState("");
  const [editBio, setEditBio] = useState("");
  const [editError, setEditError] = useState<string | null>(null);
  const [editSuccess, setEditSuccess] = useState<string | null>(null);

  // Change Password States (Lab 7)
  const [isChangingPassword, setIsChangingPassword] = useState(false);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [passwordSuccess, setPasswordSuccess] = useState<string | null>(null);
  // Logout Error State
  const [logoutError, setLogoutError] = useState<string | null>(null);

  useEffect(() => {
    if (profileQuery.error?.status === 401) {
      void signOut({ redirectTo: "/auth/login" });
    }
  }, [profileQuery.error]);

  // Handle Logout
  const handleLogout = async () => {
    setLogoutError(null);
    setIsLoggingOut(true);

    try {
      await signOut({ redirectTo: "/" });
    } catch {
      setLogoutError("Không thể kết thúc phiên đăng nhập. Vui lòng thử lại.");
      setIsLoggingOut(false);
    }
  };

  // Start Editing Profile
  const handleStartEdit = () => {
    if (!profile) return;
    setEditDisplayName(profile.displayName || "");
    setEditAvatarUrl(profile.avatarUrl || "");
    setEditBio(profile.bio || "");
    setEditError(null);
    setEditSuccess(null);
    setIsEditing(true);
    setIsChangingPassword(false);
  };

  const handleCancelEdit = () => {
    setIsEditing(false);
    setEditError(null);
  };

  // Start Change Password
  const handleStartChangePassword = () => {
    setCurrentPassword("");
    setNewPassword("");
    setConfirmPassword("");
    setPasswordError(null);
    setPasswordSuccess(null);
    setIsChangingPassword(true);
    setIsEditing(false);
  };

  const handleCancelChangePassword = () => {
    setIsChangingPassword(false);
    setPasswordError(null);
  };

  // Save Change Password
  const handleSavePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setPasswordError(null);
    setPasswordSuccess(null);

    if (!currentPassword) {
      setPasswordError("Vui lòng nhập mật khẩu hiện tại.");
      return;
    }

    if (newPassword.length < 8) {
      setPasswordError("Mật khẩu mới phải có ít nhất 8 ký tự.");
      return;
    }

    if (newPassword !== confirmPassword) {
      setPasswordError("Mật khẩu xác nhận không khớp.");
      return;
    }

    try {
      await changePasswordMutation.mutateAsync({
        currentPassword,
        newPassword,
        confirmPassword,
      });
      setPasswordSuccess("Đổi mật khẩu thành công!");
      setIsChangingPassword(false);
    } catch (err) {
      const apiErr = err as ApiError;
      if (apiErr.status === 401) {
        await signOut({ redirectTo: "/auth/login" });
        return;
      }
      if (apiErr.errors) {
        const detailList = Object.values(apiErr.errors).flat().join(" ");
        setPasswordError(detailList || apiErr.detail || "Không thể đổi mật khẩu.");
      } else {
        setPasswordError(apiErr.detail || "Không thể đổi mật khẩu. Vui lòng kiểm tra lại mật khẩu hiện tại.");
      }
    }
  };

  // Save Profile Edit
  const handleSaveProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    setEditError(null);
    setEditSuccess(null);

    const trimmedName = editDisplayName.trim();
    if (!trimmedName) {
      setEditError("Tên hiển thị không được để trống.");
      return;
    }

    if (trimmedName.length > 100) {
      setEditError("Tên hiển thị không được vượt quá 100 ký tự.");
      return;
    }

    const trimmedAvatar = editAvatarUrl.trim();
    if (trimmedAvatar && !trimmedAvatar.startsWith("http://") && !trimmedAvatar.startsWith("https://")) {
      setEditError("URL ảnh đại diện phải có giao thức http:// hoặc https://");
      return;
    }

    const trimmedBio = editBio.trim();
    if (trimmedBio.length > 500) {
      setEditError("Tiểu sử không được vượt quá 500 ký tự.");
      return;
    }

    const payload: UpdateProfileRequest = {
      displayName: trimmedName,
      avatarUrl: trimmedAvatar || null,
      bio: trimmedBio || null,
    };

    try {
      await updateProfileMutation.mutateAsync(payload);
      setIsEditing(false);
      setEditSuccess("Cập nhật hồ sơ thành công!");
    } catch (err) {
      const apiErr = err as ApiError;
      if (apiErr.status === 401) {
        await signOut({ redirectTo: "/auth/login" });
        return;
      }
      if (apiErr.errors) {
        const detailList = Object.values(apiErr.errors).flat().join(" ");
        setEditError(detailList || apiErr.detail || "Dữ liệu không hợp lệ.");
      } else {
        setEditError(apiErr.detail || "Không thể cập nhật hồ sơ. Vui lòng thử lại.");
      }
    }
  };

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
      <div className="w-full max-w-2xl bg-white rounded-3xl shadow-xl border border-gray-100 overflow-hidden">
        <div className="px-5 pt-4 sm:px-8 sm:pt-5">
          <BrandLink />
        </div>
        {/* Top Decorative Banner */}
        <div className="h-32 sm:h-40 bg-gradient-to-r from-teal-800 via-teal-700 to-orange-600 relative p-4 sm:p-6 flex items-start justify-between">
          <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-black/20 backdrop-blur-sm text-xs font-medium text-white/90">
            <ChefHat className="w-3.5 h-3.5 text-orange-300" />
            <span>Hồ sơ thành viên</span>
          </div>
        </div>

        {/* Content Container */}
        <div className="px-5 sm:px-8 pb-8 pt-0 relative">
          {/* Avatar and Action Header Row */}
          <div className="flex flex-col sm:flex-row items-center sm:items-end justify-between -mt-12 sm:-mt-16 mb-6 gap-4">
            {/* Avatar */}
            <div className="relative shrink-0">
              {profile?.avatarUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={profile.avatarUrl}
                  alt={profile.displayName || "Avatar"}
                  className="w-24 h-24 sm:w-28 sm:h-28 rounded-2xl object-cover border-4 border-white shadow-lg bg-white ring-1 ring-gray-100"
                />
              ) : (
                <div className="w-24 h-24 sm:w-28 sm:h-28 rounded-2xl bg-gradient-to-br from-orange-100 to-amber-100 border-4 border-white shadow-lg flex items-center justify-center text-orange-600 ring-1 ring-gray-100">
                  <ChefHat className="w-12 h-12 sm:w-14 sm:h-14" aria-hidden="true" />
                </div>
              )}
            </div>

            {/* Action Buttons */}
            {!loading && (
              <div className="flex flex-wrap items-center justify-center sm:justify-end gap-2 w-full sm:w-auto pt-2 sm:pt-0">
                {!isEditing && !isChangingPassword && (
                  <>
                    <button
                      type="button"
                      id="edit-profile-button"
                      onClick={handleStartEdit}
                      aria-label="Chỉnh sửa thông tin hồ sơ"
                      className="inline-flex items-center justify-center px-3.5 py-2 rounded-xl border border-orange-200 bg-orange-50 text-orange-700 hover:bg-orange-100 hover:border-orange-300 font-medium text-xs sm:text-sm transition focus:outline-none focus:ring-2 focus:ring-orange-400 shadow-sm"
                    >
                      <Pencil className="w-3.5 h-3.5 mr-1.5" aria-hidden="true" />
                      Chỉnh sửa
                    </button>

                    <button
                      type="button"
                      id="change-password-button"
                      onClick={handleStartChangePassword}
                      aria-label="Đổi mật khẩu tài khoản"
                      className="inline-flex items-center justify-center px-3.5 py-2 rounded-xl border border-gray-200 bg-gray-50 text-gray-700 hover:bg-gray-100 hover:border-gray-300 font-medium text-xs sm:text-sm transition focus:outline-none focus:ring-2 focus:ring-gray-400 shadow-sm"
                    >
                      <KeyRound className="w-3.5 h-3.5 mr-1.5" aria-hidden="true" />
                      Đổi mật khẩu
                    </button>
                  </>
                )}

                <button
                  type="button"
                  id="logout-button"
                  onClick={handleLogout}
                  disabled={isLoggingOut}
                  aria-label="Đăng xuất khỏi hệ thống"
                  className="inline-flex items-center justify-center px-3.5 py-2 rounded-xl border border-red-200 text-red-600 hover:bg-red-50 hover:border-red-300 font-medium text-xs sm:text-sm transition focus:outline-none focus:ring-2 focus:ring-red-400 disabled:opacity-60 disabled:cursor-not-allowed shadow-sm"
                >
                  {isLoggingOut ? (
                    <>
                      <Loader2 className="w-3.5 h-3.5 mr-1.5 animate-spin" aria-hidden="true" />
                      Đang đăng xuất...
                    </>
                  ) : (
                    <>
                      <LogOut className="w-3.5 h-3.5 mr-1.5" aria-hidden="true" />
                      Đăng xuất
                    </>
                  )}
                </button>
              </div>
            )}
          </div>

          {/* Feedback Alerts */}
          {editSuccess && (
            <div
              className="mb-5 flex items-start gap-3 rounded-xl bg-green-50 border border-green-200 p-3.5 text-sm text-green-800"
              role="status"
            >
              <CheckCircle2 className="w-5 h-5 text-green-600 shrink-0 mt-0.5" aria-hidden="true" />
              <div className="flex-1 font-medium">{editSuccess}</div>
            </div>
          )}

          {passwordSuccess && (
            <div
              className="mb-5 flex items-start gap-3 rounded-xl bg-green-50 border border-green-200 p-3.5 text-sm text-green-800"
              role="status"
            >
              <CheckCircle2 className="w-5 h-5 text-green-600 shrink-0 mt-0.5" aria-hidden="true" />
              <div className="flex-1 font-medium">{passwordSuccess}</div>
            </div>
          )}

          {logoutError && (
            <div
              className="mb-5 flex items-start gap-3 rounded-xl bg-red-50 border border-red-200 p-3.5 text-sm text-red-800"
              role="alert"
            >
              <AlertCircle className="w-5 h-5 text-red-600 shrink-0 mt-0.5" aria-hidden="true" />
              <div className="flex-1 font-medium">{logoutError}</div>
              <button
                type="button"
                onClick={() => setLogoutError(null)}
                className="text-red-500 hover:text-red-700 p-1 rounded-lg focus:outline-none"
                aria-label="Đóng thông báo"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          )}

          {/* Body Content */}
          {loading ? (
            <div className="py-12 flex flex-col items-center justify-center space-y-3" role="status">
              <Loader2 className="w-8 h-8 text-orange-600 animate-spin" />
              <p className="text-sm text-gray-500 font-medium">
                Đang tải thông tin hồ sơ...
              </p>
            </div>
          ) : error ? (
            <div className="space-y-5 py-4">
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
                  onClick={() => { void profileQuery.refetch(); }}
                  className="inline-flex items-center px-4 py-2 rounded-xl text-white font-medium bg-orange-600 hover:bg-orange-700 transition shadow-sm text-sm"
                >
                  <RefreshCw className="w-4 h-4 mr-2" />
                  Thử lại
                </button>
              </div>
            </div>
          ) : profile && isEditing ? (
            /* EDIT PROFILE FORM */
            <form onSubmit={handleSaveProfile} className="space-y-4 pt-2">
              <div className="border-b border-gray-100 pb-3">
                <h2 className="text-lg font-bold text-gray-900">
                  Chỉnh sửa hồ sơ cá nhân
                </h2>
                <p className="text-xs text-gray-500 mt-0.5">
                  Cập nhật thông tin hiển thị của bạn trên Culinary Blog.
                </p>
              </div>

              {editError && (
                <div
                  className="flex items-start gap-3 rounded-xl bg-red-50 border border-red-200 p-3 text-sm text-red-800"
                  role="alert"
                >
                  <AlertCircle className="w-4 h-4 text-red-600 shrink-0 mt-0.5" aria-hidden="true" />
                  <div className="flex-1">{editError}</div>
                </div>
              )}

              <div className="space-y-1">
                <label
                  htmlFor="edit-display-name"
                  className="block text-xs font-semibold text-gray-700 uppercase"
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
                  className="w-full px-3.5 py-2 rounded-xl border border-gray-300 text-gray-900 text-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500 transition shadow-sm"
                />
              </div>

              <div className="space-y-1">
                <label
                  htmlFor="edit-avatar-url"
                  className="block text-xs font-semibold text-gray-700 uppercase"
                >
                  Avatar URL (Liên kết ảnh)
                </label>
                <input
                  type="url"
                  id="edit-avatar-url"
                  name="avatarUrl"
                  value={editAvatarUrl}
                  onChange={(e) => setEditAvatarUrl(e.target.value)}
                  placeholder="https://example.com/avatar.jpg"
                  className="w-full px-3.5 py-2 rounded-xl border border-gray-300 text-gray-900 text-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500 transition shadow-sm"
                />
              </div>

              <div className="space-y-1">
                <label
                  htmlFor="edit-bio"
                  className="block text-xs font-semibold text-gray-700 uppercase"
                >
                  Tiểu sử cá nhân
                </label>
                <textarea
                  id="edit-bio"
                  name="bio"
                  value={editBio}
                  onChange={(e) => setEditBio(e.target.value)}
                  placeholder="Chia sẻ một chút về niềm đam mê ẩm thực của bạn..."
                  rows={3}
                  maxLength={500}
                  className="w-full px-3.5 py-2 rounded-xl border border-gray-300 text-gray-900 text-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500 transition shadow-sm"
                />
              </div>

              <div className="flex items-center justify-end gap-2.5 pt-2 border-t border-gray-100">
                <button
                  type="button"
                  id="cancel-edit-button"
                  onClick={handleCancelEdit}
                  disabled={isSaving}
                  className="px-4 py-2 rounded-xl border border-gray-300 text-gray-700 hover:bg-gray-50 font-medium text-xs sm:text-sm transition focus:outline-none focus:ring-2 focus:ring-gray-300 disabled:opacity-60"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  id="save-profile-button"
                  disabled={isSaving}
                  className="inline-flex items-center justify-center px-4 py-2 rounded-xl bg-orange-600 hover:bg-orange-700 text-white font-medium text-xs sm:text-sm transition focus:outline-none focus:ring-2 focus:ring-orange-500 disabled:opacity-60 shadow-sm"
                >
                  {isSaving ? (
                    <>
                      <Loader2 className="w-4 h-4 mr-1.5 animate-spin" />
                      Đang lưu...
                    </>
                  ) : (
                    <>
                      <Save className="w-4 h-4 mr-1.5" />
                      Lưu thay đổi
                    </>
                  )}
                </button>
              </div>
            </form>
          ) : profile && isChangingPassword ? (
            /* CHANGE PASSWORD FORM */
            <form onSubmit={handleSavePassword} className="space-y-4 pt-2">
              <div className="border-b border-gray-100 pb-3">
                <h2 className="text-lg font-bold text-gray-900">
                  Đổi mật khẩu tài khoản
                </h2>
                <p className="text-xs text-gray-500 mt-0.5">
                  Áp dụng cho tài khoản sử dụng mật khẩu hệ thống (Local).
                </p>
              </div>

              {passwordError && (
                <div
                  className="flex items-start gap-3 rounded-xl bg-red-50 border border-red-200 p-3 text-sm text-red-800"
                  role="alert"
                >
                  <AlertCircle className="w-4 h-4 text-red-600 shrink-0 mt-0.5" aria-hidden="true" />
                  <div className="flex-1">{passwordError}</div>
                </div>
              )}

              <div className="space-y-1">
                <label
                  htmlFor="current-password"
                  className="block text-xs font-semibold text-gray-700 uppercase"
                >
                  Mật khẩu hiện tại <span className="text-red-500">*</span>
                </label>
                <input
                  type="password"
                  id="current-password"
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                  placeholder="Nhập mật khẩu hiện tại..."
                  required
                  className="w-full px-3.5 py-2 rounded-xl border border-gray-300 text-gray-900 text-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500 transition shadow-sm"
                />
              </div>

              <div className="space-y-1">
                <label
                  htmlFor="new-password"
                  className="block text-xs font-semibold text-gray-700 uppercase"
                >
                  Mật khẩu mới <span className="text-red-500">*</span>
                </label>
                <input
                  type="password"
                  id="new-password"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  placeholder="Tối thiểu 8 ký tự..."
                  required
                  className="w-full px-3.5 py-2 rounded-xl border border-gray-300 text-gray-900 text-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500 transition shadow-sm"
                />
              </div>

              <div className="space-y-1">
                <label
                  htmlFor="confirm-password"
                  className="block text-xs font-semibold text-gray-700 uppercase"
                >
                  Xác nhận mật khẩu mới <span className="text-red-500">*</span>
                </label>
                <input
                  type="password"
                  id="confirm-password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder="Nhập lại mật khẩu mới..."
                  required
                  className="w-full px-3.5 py-2 rounded-xl border border-gray-300 text-gray-900 text-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500 transition shadow-sm"
                />
              </div>

              <div className="flex items-center justify-end gap-2.5 pt-2 border-t border-gray-100">
                <button
                  type="button"
                  id="cancel-password-button"
                  onClick={handleCancelChangePassword}
                  disabled={isSavingPassword}
                  className="px-4 py-2 rounded-xl border border-gray-300 text-gray-700 hover:bg-gray-50 font-medium text-xs sm:text-sm transition focus:outline-none focus:ring-2 focus:ring-gray-300 disabled:opacity-60"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  id="save-password-button"
                  disabled={isSavingPassword}
                  className="inline-flex items-center justify-center px-4 py-2 rounded-xl bg-orange-600 hover:bg-orange-700 text-white font-medium text-xs sm:text-sm transition focus:outline-none focus:ring-2 focus:ring-orange-500 disabled:opacity-60 shadow-sm"
                >
                  {isSavingPassword ? (
                    <>
                      <Loader2 className="w-4 h-4 mr-1.5 animate-spin" />
                      Đang lưu...
                    </>
                  ) : (
                    <>
                      <Save className="w-4 h-4 mr-1.5" />
                      Cập nhật mật khẩu
                    </>
                  )}
                </button>
              </div>
            </form>
          ) : profile ? (
            /* VIEW PROFILE DETAILS */
            <div className="space-y-5">
              {/* Name & Roles */}
              <div className="text-center sm:text-left">
                <h1 className="text-2xl sm:text-3xl font-bold text-gray-900 tracking-tight">
                  {profile.displayName}
                </h1>
                {profile.bio && (
                  <p className="text-sm text-gray-600 mt-1 italic">
                    &ldquo;{profile.bio}&rdquo;
                  </p>
                )}
                {/* Roles Badges */}
                <div className="flex flex-wrap justify-center sm:justify-start gap-1.5 mt-3">
                  {profile.roles && profile.roles.length > 0 ? (
                    profile.roles.map((role) => (
                      <span
                        key={role}
                        className="inline-flex items-center px-2.5 py-0.5 rounded-lg text-xs font-semibold bg-orange-50 text-orange-700 border border-orange-200"
                      >
                        <Shield className="w-3 h-3 mr-1" />
                        {role}
                      </span>
                    ))
                  ) : (
                    <span className="inline-flex items-center px-2.5 py-0.5 rounded-lg text-xs font-medium bg-gray-100 text-gray-700">
                      Thành viên
                    </span>
                  )}
                </div>
              </div>

              {/* Information Cards */}
              <div className="rounded-2xl border border-gray-100 bg-gray-50/70 p-4 space-y-3">
                {/* Email Item */}
                <div className="flex flex-col sm:flex-row sm:items-center justify-between text-sm gap-1 sm:gap-2">
                  <div className="flex items-center text-gray-500">
                    <Mail className="w-4 h-4 mr-2 text-gray-400 shrink-0" />
                    <span>Địa chỉ Email</span>
                  </div>
                  <div className="flex items-center gap-1.5 font-medium text-gray-800 break-all">
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

                {/* Member Since Item */}
                <div className="flex flex-col sm:flex-row sm:items-center justify-between text-sm gap-1 sm:gap-2 pt-2 border-t border-gray-200/60">
                  <div className="flex items-center text-gray-500">
                    <Calendar className="w-4 h-4 mr-2 text-gray-400 shrink-0" />
                    <span>Ngày tham gia</span>
                  </div>
                  <span className="font-medium text-gray-800">
                    {formatDate(profile.createdAt)}
                  </span>
                </div>
              </div>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}
