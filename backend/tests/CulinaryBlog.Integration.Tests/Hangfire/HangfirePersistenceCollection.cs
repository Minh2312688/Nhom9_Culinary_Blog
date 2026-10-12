using Xunit;

namespace CulinaryBlog.Integration.Tests.Hangfire;

/// <summary>
/// Both Hangfire persistence tests share one isolated PostgreSQL instance via
/// HANGFIRE_PERSISTENCE_CONNECTION. Placing them in a single collection makes
/// xunit run them sequentially instead of in parallel: two concurrent
/// BackgroundJobServers on the same storage/queue could dequeue each other's
/// jobs and orphan them in Processing when disposed mid-run.
/// </summary>
[CollectionDefinition("HangfirePersistence")]
public sealed class HangfirePersistenceCollection
{
}
