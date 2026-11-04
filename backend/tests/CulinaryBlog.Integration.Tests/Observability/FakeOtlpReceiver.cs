using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace CulinaryBlog.Integration.Tests.Observability;

public sealed class FakeOtlpReceiver : IAsyncDisposable
{
    private readonly WebApplication _app;
    public string Url { get; }
    public List<ReceivedRequest> Requests { get; } = new();
    public int DelayMs { get; set; }

    public sealed class ReceivedRequest
    {
        public string Path { get; set; } = "";
        public Dictionary<string, string> Headers { get; set; } = new();
        public bool Aborted { get; set; }
    }

    public FakeOtlpReceiver()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _app = builder.Build();
        _app.MapPost("/{**path}", async (HttpContext ctx) =>
        {
            var req = new ReceivedRequest { Path = ctx.Request.Path.Value ?? "" };
            foreach (var h in ctx.Request.Headers)
            {
                req.Headers[h.Key] = h.Value.ToString();
            }
            Requests.Add(req);
            if (DelayMs > 0)
            {
                try { await Task.Delay(DelayMs, ctx.RequestAborted); }
                catch (TaskCanceledException) { req.Aborted = true; }
            }
            if (ctx.RequestAborted.IsCancellationRequested) req.Aborted = true;
        });
        _app.StartAsync().GetAwaiter().GetResult();
        Url = _app.Urls.First();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
