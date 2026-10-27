using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Health;

/// <summary>
/// PostgreSQL readiness probe. It only opens a connection: no migration and
/// no seeding, so a reachable but un-migrated database still counts as healthy
/// and the probe has no side effects.
/// </summary>
public sealed class PostgresProbe : IPostgresProbe
{
    private readonly ApplicationDbContext db;

    public PostgresProbe(ApplicationDbContext db)
    {
        this.db = db;
    }

    public Task<bool> ProbeAsync(CancellationToken ct) => db.Database.CanConnectAsync(ct);
}