namespace CulinaryBlog.Application.Common.Models;

public sealed record BaseStatusDto(
    string Application,
    string Environment,
    DateTimeOffset TimestampUtc);
