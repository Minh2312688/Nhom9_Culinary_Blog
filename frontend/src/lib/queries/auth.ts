import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  changePasswordApi,
  forgotPasswordApi,
  getProfileApi,
  getAdminUsersApi,
  resetPasswordApi,
  updateProfileApi,
} from "@/lib/auth-api";
import {
  registerApi,
  type ApiError,
  type RegisterPayload,
  type RegisterResult,
} from "@/lib/api";
import type {
  ChangePasswordRequest,
  ForgotPasswordRequest,
  ResetPasswordRequest,
  UpdateProfileRequest,
  UserProfile,
  AdminUser,
} from "@/lib/auth-api";
import { queryKeys } from "./query-keys";

export function useProfile(userId: string, enabled = true) {
  return useQuery<UserProfile, ApiError>({
    queryKey: queryKeys.profile.detail(userId),
    queryFn: ({ signal }) => getProfileApi(signal),
    enabled: enabled && Boolean(userId),
  });
}

export function useAdminUsers(enabled = true) {
  return useQuery<AdminUser[], ApiError>({
    queryKey: queryKeys.admin.users(),
    queryFn: ({ signal }) => getAdminUsersApi(signal),
    enabled,
  });
}

export function useUpdateProfile(userId: string) {
  const queryClient = useQueryClient();
  return useMutation<UserProfile, ApiError, UpdateProfileRequest>({
    mutationFn: updateProfileApi,
    onSuccess: (profile) => {
      queryClient.setQueryData(queryKeys.profile.detail(userId), profile);
    },
  });
}

export function useChangePassword() {
  return useMutation<{ message: string }, ApiError, ChangePasswordRequest>({
    mutationFn: changePasswordApi,
  });
}

export function useRegisterAccount() {
  return useMutation<RegisterResult, ApiError, RegisterPayload>({
    mutationFn: registerApi,
  });
}

export function useForgotPassword() {
  return useMutation<{ message: string }, ApiError, ForgotPasswordRequest>({
    mutationFn: forgotPasswordApi,
  });
}

export function useResetPassword() {
  return useMutation<{ message: string }, ApiError, ResetPasswordRequest>({
    mutationFn: resetPasswordApi,
  });
}
