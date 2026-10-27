# Quy trình Xoay Vòng Khóa Ký JWT (90-Day JWT Signing Key Rotation)
**Dự án:** Culinary Blog – Phát triển Ứng dụng Web Nâng cao (Nhóm 9)
**Tiêu chuẩn:** SRS v1.0.0 §4.2 trang 42 – `NFR-SEC-007`
**Chủ sở hữu (Owner):** TV1 – Authentication, Security, Base Infrastructure (Nguyễn Phạm Phú Nam – MSSV: 2312695)

---

## 1. Mục Đích & Chu Kỳ

Theo yêu cầu phi chức năng bảo mật **NFR-SEC-007** trong SRS v1.0.0:
- Khóa bí mật ký JWT (`Jwt:Key`) phải có độ dài tối thiểu 256 bits (32 bytes) dùng cho thuật toán HMAC-SHA256 (`HS256`).
- Khuyến nghị xoay vòng (rotate) khóa ký định kỳ **mỗi 90 ngày (quý/lần)** hoặc ngay lập tức khi phát hiện nghi vấn lộ lọt thông tin bí mật.
- Quá trình xoay vòng phải diễn ra an toàn, không làm gián đoạn trải nghiệm người dùng hợp lệ và không làm vô hiệu hóa hàng loạt phiên làm việc.

---

## 2. Kiến Trúc & Cơ Chế Hoạt Động

### 2.1. Phân Tách Giữa Access Token và Refresh Token
- **Access Token:** Có thời gian sống ngắn (**15 phút** theo `NFR-SEC-002`). Được ký bằng `Jwt:Key` (HMAC-SHA256). Token này là stateless, client gửi kèm `Authorization: Bearer <token>`.
- **Refresh Token:** Có thời gian sống **7 ngày** (`NFR-SEC-002`), được tạo ngẫu nhiên bằng `RandomNumberGenerator` an toàn mã hóa và lưu trữ dạng **SHA-256 hash** trong bảng `RefreshTokens` của PostgreSQL.
- **Tính độc lập:** Việc thay đổi `Jwt:Key` **không làm hỏng dữ liệu refresh token** trong cơ sở dữ liệu. Khi Access Token cũ hết hạn hoặc bị từ chối sau rotation, client tự động gọi endpoint `/api/auth/refresh` bằng Refresh Token để nhận cặp token mới được ký bằng khóa mới.

### 2.2. Chiến Lược Chuyển Đổi Kép (Dual-Key Rollover Window)
Để giảm thiểu tối đa hiện tượng người dùng bị đăng xuất đột ngột trong thời gian chuyển tiếp:
1. **Giai đoạn chuẩn bị (T - 15 phút):**
   - Sinh khóa bí mật mới $K_{new}$ (256-bit cryptographically secure string).
   - Cấu hình API chấp nhận cả $K_{new}$ và $K_{old}$ trong danh sách `IssuerSigningKeys` của `TokenValidationParameters`.
   - Khóa $K_{old}$ tiếp tục được dùng để ký token mới, trong khi hệ thống xác thực chấp nhận cả hai.
2. **Giai đoạn chuyển giao (T = 0):**
   - Thiết lập $K_{new}$ làm khóa ký chính (`IssuerSigningKey` cho việc sinh token mới).
   - $K_{old}$ chuyển thành khóa phụ chỉ dùng để xác minh (verify-only).
   - Mọi token mới sinh ra sẽ mang chữ ký từ $K_{new}$.
3. **Giai đoạn kết thúc (T + 15 phút):**
   - Vì TTL của Access Token tối đa là 15 phút, sau 15 phút toàn bộ Access Token ký bằng $K_{old}$ đều đã hết hạn tự nhiên.
   - Gỡ bỏ hoàn toàn $K_{old}$ khỏi cấu hình. Hệ thống chuyển sang chỉ sử dụng $K_{new}$.

---

## 3. Các Bước Thực Hiện Vận Hành (Operational Playbook)

### Bước 1: Sinh Khóa Mới An Toàn
Sử dụng PowerShell hoặc CLI chuyên dụng để sinh chuỗi ngẫu nhiên 64 ký tự (256 bits trở lên):
```powershell
# PowerShell: Sinh chuỗi ngẫu nhiên cryptographically secure
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$newKey = [Convert]::ToBase64String($bytes)
Write-Output "Generated Key (Keep secret): $newKey"
```

### Bước 2: Cập Nhật Cấu Hình Theo Từng Môi Trường

#### A. Môi Trường Development (Local)
Tuyệt đối không commit key vào `appsettings.json` hoặc Git. Sử dụng **ASP.NET Core User Secrets**:
```powershell
cd backend/src/CulinaryBlog.API
dotnet user-secrets set "Jwt:Key" "<Gia_Tri_Khoa_Moi>"
```

#### B. Môi Trường Production / Docker Compose
Cập nhật biến môi trường qua file `.env` (được bảo vệ bởi `.gitignore` và phân quyền file hệ thống):
```bash
# Trong file .env của server production
JWT_SECRET=<Gia_Tri_Khoa_Moi>
```
Khởi động lại service API qua rolling update hoặc container restart:
```bash
docker compose up -d --no-deps api
```

### Bước 3: Kiểm Tra Sau Khi Xoay Vòng
1. **Kiểm tra đăng nhập:** Đăng nhập tài khoản kiểm thử, xác nhận nhận được access token hợp lệ.
2. **Kiểm tra truy cập API:** Gọi endpoint `/api/auth/me` với token mới, xác nhận trả về `200 OK`.
3. **Kiểm tra Refresh Token:** Gọi `/api/auth/refresh` với refresh token hiện có, xác nhận token mới được cấp phát thành công.
4. **Kiểm tra Token cũ:** Xác nhận token ký bằng khóa cũ bị từ chối `401 Unauthorized` sau khi gỡ bỏ $K_{old}$.

---

## 4. Kế Hoạch Khôi Phục Sự Cố (Rollback Plan)

Nếu sau khi cập nhật khóa mới phát sinh lỗi hệ thống không mong muốn:
1. **Lập tức khôi phục giá trị khóa cũ $K_{old}$** vào biến môi trường `JWT_SECRET` hoặc User Secrets.
2. **Khởi động lại API container:** `docker compose restart api`.
3. Do cơ sở dữ liệu `RefreshTokens` không bị sửa đổi, phiên đăng nhập của người dùng sẽ hoạt động bình thường trở lại ngay lập tức.
4. Điều tra nguyên nhân trên môi trường Staging/Dev trước khi lên lịch xoay vòng tiếp theo.

---

## 5. Lịch Trình Xoay Vòng Khóa (Audit Schedule)

| Đợt | Ngày Dự Kiến | Người Thực Hiện | Trạng Thái | Ghi Chú |
|---|---|---|---|---|
| Q1 / 2026 | 15/01/2026 | TV1 (Nam) | Đã hoàn thành | Khóa ban đầu giai đoạn khởi tạo |
| Q2 / 2026 | 15/04/2026 | TV1 (Nam) | Đã hoàn thành | Chu kỳ quý 2 |
| Q3 / 2026 | 15/07/2026 | TV1 (Nam) | Đã hoàn thành | Chu kỳ quý 3 |
| Q4 / 2026 | 15/10/2026 | TV1 (Nam) | Lên lịch (Week 6) | Chu kỳ quý 4 chuẩn bị bàn giao |
