namespace CulinaryBlog.Application.Common.Models;
/// <summary>
/// Generic wrapper cho mọi kết quả phân trang trong hệ thống.
/// Chứa đủ metadata để client tự xây dựng navigation links.
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của từng item trong danh sách</typeparam>
public sealed class PaginatedResult<T>
{
 // Danh sách items trong trang hiện tại
 public IReadOnlyList<T> Items { get; init; }
 // Tổng số bản ghi (không phân trang) — cần để tính TotalPages
 public int TotalCount { get; init; }
 // Trang hiện tại (bắt đầu từ 1)
 public int Page { get; init; }
 // Số item trên một trang
 public int PageSize { get; init; }
 // Tính toán từ các giá trị trên — không lưu trong database
 public int TotalPages => (int)Math.Ceiling(TotalCount /
(double)PageSize);
 public bool HasNextPage => Page < TotalPages;
 public bool HasPreviousPage => Page > 1;
 public PaginatedResult(
 IReadOnlyList<T> items,
 int totalCount, int page, int pageSize)
 {
 Items = items;
 TotalCount = totalCount;
 Page = page;
 PageSize = pageSize;
 }
}
