# Đề Xuất Tích Hợp: Quản Lý Quyền Sở Hữu Tài Nguyên Recipe (Resource Ownership)
**Dự án:** Culinary Blog – Phát triển Ứng dụng Web Nâng cao (Nhóm 9)
**Tiêu chuẩn đối chiếu:** SRS v1.0.0 §4.2 trang 42 – `NFR-SEC-006`
**Người lập đề xuất:** TV1 – Nguyễn Phạm Phú Nam (MSSV: 2312695 – Auth & Security)
**Người nhận đề xuất:** TV2 – Dương Văn Minh (Owner module Recipe)

---

## 1. Tóm Tắt Hiện Trạng & Đánh Giá

### 1.1. Hiện trạng triển khai của TV2
Trong module Recipe (`RecipeCommands.cs`, `RecipeStepCommands.cs`), TV2 đã thực hiện kiểm tra quyền sở hữu tại **Application Layer**:
- `EnsureOwner(Recipe recipe)`: Kiểm tra `if (!currentUser.IsAdmin && recipe.AuthorId != currentUser.UserId) throw new ForbiddenAccessException();`
- `GetOwnedRecipe(Guid id, CancellationToken ct)`: Nạp Recipe và kiểm tra điều kiện tương tự.
- `RecipeStepCommandHelpers.GetEditableRecipeAsync(...)`: Kiểm tra trước khi thêm/sửa/xóa RecipeStep.

### 1.2. Đánh giá bảo mật
- **Mục tiêu bảo mật:** ĐÃ ĐẠT. Thao tác ghi/sửa/xóa Recipe và RecipeStep đều bị chặn ở Application Layer với lỗi `ForbiddenAccessException` (map ra `HTTP 403 Forbidden`) nếu tác giả không phải chủ sở hữu và không có quyền Admin.
- **Điểm khác biệt so với SRS v1.0.0:** SRS ghi chú kỹ thuật: *"ASP.NET Core AuthorizationHandler (RecipeAuthorizationHandler)"*. Hiện tại TV2 đang kiểm tra dạng imperative check trong Command Handler thay vì đăng ký `IAuthorizationHandler<OperationAuthorizationRequirement, Recipe>`.

---

## 2. Phương Án Tối Ưu (Nếu TV2 Đồng Ý Refactor)

Nếu TV2 muốn chuẩn hóa theo đúng kiến trúc ASP.NET Core Resource-Based Authorization:

### Bước 1: Tạo `RecipeAuthorizationHandler`
```csharp
namespace CulinaryBlog.Application.Features.Recipes.Security;

using System.Security.Claims;
using System.Threading.Tasks;
using CulinaryBlog.Domain.Constants;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

public sealed class RecipeOperations
{
    public static readonly OperationAuthorizationRequirement Create = new() { Name = nameof(Create) };
    public static readonly OperationAuthorizationRequirement Read = new() { Name = nameof(Read) };
    public static readonly OperationAuthorizationRequirement Update = new() { Name = nameof(Update) };
    public static readonly OperationAuthorizationRequirement Delete = new() { Name = nameof(Delete) };
}

public sealed class RecipeAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, Recipe>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        Recipe resource)
    {
        // Admin luôn có toàn quyền
        if (context.User.IsInRole(AppRoles.Admin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Tác giả chỉ được sửa/xóa công thức do mình tạo
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;

        if (userId != null && resource.AuthorId == userId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```

### Bước 2: Tích Hợp Vào Command Handler
Trong `UpdateRecipeCommandHandler` hoặc `RecipeLifecycleHandler`:
```csharp
var authResult = await authorizationService.AuthorizeAsync(
    currentUserPrincipal,
    recipe,
    RecipeOperations.Update);

if (!authResult.Succeeded)
{
    throw new ForbiddenAccessException();
}
```

---

## 3. Khuyến Nghị Phối Hợp
1. **Trước mắt (Week 6):** Giữ nguyên implementation hiện tại của TV2 để bảo đảm tính ổn định, không làm gián đoạn các test nghiệp vụ Recipe hiện có (tất cả 744 tests đang PASS).
2. **Kế hoạch tiếp theo:** TV2 xem xét đề xuất này để quyết định có chuyển đổi sang `RecipeAuthorizationHandler` trong giai đoạn sau hay không.
3. TV1 cam kết hỗ trợ TV2 viết test cases cho `RecipeAuthorizationHandler` khi TV2 tiến hành tích hợp.
