namespace CulinaryBlog.Application.DTOs.System;

public sealed record BaseStatusDto(
    string Application,
    string Environment,
    DateTimeOffset TimestampUtc);
