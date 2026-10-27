using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs.Recipes;

namespace CulinaryBlog.Application.DTOs.Authors;

public sealed record AuthorProfilePageDto(
    string Id,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    PaginatedResult<RecipeSummaryDto> Recipes);
