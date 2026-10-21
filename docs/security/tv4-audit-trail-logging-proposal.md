# Đề Xuất Tích Hợp: Ghi Nhận Nhật Ký Kiểm Toán (Audit Trail Logging)
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
3. **Cơ chế ghi log hiện tại:** Serilog ghi log hệ thống và HTTP requests, nhưng chưa có middleware hoặc pipeline behavior chuyên biệt tự động gắn `UserId` và `Timestamp` vào metadata của mọi Write Command.

---

## 3. Thiết Kế Đề Xuất Cho TV4

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

## 4. Kế Hoạch Phối Hợp
- TV4 xem xét đề xuất và tiến hành đăng ký `AuditLoggingBehavior` vào MediatR pipeline trong `CulinaryBlog.Application/DependencyInjection.cs` hoặc cấu hình Serilog Enricher.
- TV1 sẵn sàng hỗ trợ TV4 kiểm thử unit/integration test cho cơ chế audit log.
