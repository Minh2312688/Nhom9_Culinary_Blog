# Đề Xuất Tích Hợp: Ghi Nhận Nhật Ký Kiểm Toán (Audit Trail Logging)

> **Trạng thái hiện tại (17/11/2026):** `AuditLoggingBehavior` ghi audit cho
> mọi MediatR request có tên `*Command`, gồm Auth, Category và Recipe commands.
> Event được ghi cho thành công, thất bại (kèm loại exception) và hủy; lỗi được
> rethrow. Behavior nằm ngoài validation để ghi nhận cả validation failure.
> Chỉ metadata được ghi, không có command payload, exception message hay
> credentials. `IAuditCommand` hiện là metadata bổ sung tùy chọn.

**Dự án:** Culinary Blog – Phát triển Ứng dụng Web Nâng cao (Nhóm 9)
**Tiêu chuẩn đối chiếu:** SRS v1.0.0 §4.2 trang 42 – `NFR-SEC-006`
**Người lập đề xuất:** TV1 – Nguyễn Phạm Phú Nam (MSSV: 2312695 – Auth & Security)
**Người nhận đề xuất:** TV4 – Mai Quý Phước (Owner module Serilog / Logging / Observability)

---

## 1. Yêu Cầu Theo SRS v1.0.0

Mục §4.2 trang 42 (`NFR-SEC-006`) yêu cầu:
- *"Ghi nhận audit log cho mọi thao tác ghi (write operations: POST, PUT, PATCH, DELETE) kèm `userId` và `timestamp` bằng Serilog."*
- Bảo đảm nguyên tắc an toàn dữ liệu: Tuyệt đối không log thông tin nhạy cảm (mật khẩu, plain token, khóa ký JWT, thông tin thẻ/tài khoản).

---

## 2. Phân Tích Hiện Trạng

1. **Module Serilog:** Do TV4 quản lý cấu hình trong `CulinaryBlog.Infrastructure`.
2. **Ngữ cảnh người dùng (Current User Context):** TV1 đã xây dựng abstraction `ICurrentUserService` nạp `UserId`, `IsAdmin`, `IsAuthenticated` từ `IHttpContextAccessor`.
3. **Cơ chế ghi log hiện tại:** `AuditLoggingBehavior` dùng `ILogger` (được cấu hình qua Serilog trong API) để ghi action, `UserId`, UTC timestamp và outcome cho mọi MediatR `*Command`. Query được bỏ qua; request payload và exception message không được ghi.

---

## 3. Cơ Chế Đang Triển Khai

- `AuditLoggingBehavior` nhận diện command theo quy ước tên `*Command`; không phụ thuộc vào marker `IAuditCommand`, vì vậy command mới được audit tự động.
- Audit behavior đăng ký trước `ValidationBehavior`, nên bao phủ command validation thất bại cũng như lỗi từ handler.
- Thành công ghi `Information`; thất bại ghi `Warning`; cancellation ghi `Information`. Lỗi được rethrow.
- Event có action, entity metadata nếu command implement `IAuditCommand`, user ID (có thể null với command ẩn danh), timestamp UTC và outcome. Với failure chỉ ghi tên loại exception, không ghi exception object/message hay request payload.
- Request bị chặn trước MediatR (ví dụ lỗi HTTP binding/authorization) không thuộc audit command pipeline.
- `IAuditTrail` cũng được gọi riêng cho thao tác tạo/cập nhật/xoay vòng refresh token và job xử lý variants ảnh. Đây là các điểm tích hợp cụ thể, không phải audit tổng quát cho mọi HTTP write hoặc mọi ghi dữ liệu trong hệ thống.
- Test coverage hiện có: [`AuditLoggingBehaviorTests.cs`](../../backend/tests/CulinaryBlog.Application.Tests/Common/AuditLoggingBehaviorTests.cs), [`AuditTrailTests.cs`](../../backend/tests/CulinaryBlog.Application.Tests/Common/AuditTrailTests.cs) và [`RefreshTokenRepositoryAuditTests.cs`](../../backend/tests/CulinaryBlog.Application.Tests/Infrastructure/Authentication/RefreshTokenRepositoryAuditTests.cs). Các file xác nhận test cases có trong repository; không hàm ý đã chạy lại trong lần cập nhật tài liệu.

## 4. Thiết Kế Ban Đầu (Lịch Sử)

> Code và kế hoạch phối hợp bên dưới lưu lại proposal ban đầu; implementation hiện tại được mô tả ở mục 3.

### Phương Án: MediatR Pipeline Behavior (`AuditLoggingBehavior`)
Thực hiện intercept mọi command (write operation) trước/sau khi thực thi:

```csharp
namespace CulinaryBlog.Application.Common.Behaviors;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CulinaryBlog.Application.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

public sealed class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;

    public AuditLoggingBehavior(
        ICurrentUserService currentUserService,
        ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    {
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Chỉ log các command (thao tác ghi), bỏ qua Query
        if (requestName.EndsWith("Command", StringComparison.OrdinalIgnoreCase))
        {
            var userId = _currentUserService.UserId ?? "Anonymous";
            var timestamp = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "[AUDIT TRAIL] Action: {CommandName} | UserId: {UserId} | Timestamp: {Timestamp:O}",
                requestName,
                userId,
                timestamp);
        }

        return await next();
    }
}
```

### Ưu Điểm Của Phương Án:
- **Tự động hóa 100%:** Mọi Command mới thêm vào hệ thống (Recipe, Category, Auth, User) đều tự động được ghi nhật ký audit trail.
- **Tuân thủ Clean Architecture:** Tách biệt rõ ràng với Presentation Layer.
- **An toàn dữ liệu:** Không dump payload của request nên không bao giờ làm lộ mật khẩu (ví dụ `RegisterCommand`, `LoginCommand`).

---

## 5. Xác Minh

`dotnet test backend\tests\CulinaryBlog.Application.Tests\CulinaryBlog.Application.Tests.csproj --no-restore` đạt 813/813. Coverage audit kiểm tra command có/không có `IAuditCommand`, `LogoutCommand` không lộ refresh token, success/failure/cancellation, validation failure qua MediatR, query exclusion, và không log password hay exception message.
