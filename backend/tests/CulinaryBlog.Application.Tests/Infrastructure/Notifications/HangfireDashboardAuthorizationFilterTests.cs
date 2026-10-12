using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CulinaryBlog.Domain.Constants;
using CulinaryBlog.Infrastructure.Notifications;
using FluentAssertions;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

public sealed class HangfireDashboardAuthorizationFilterTests
{
    private const string Key = "UnitTestSigningKey_ForDashboardFilter_AndIntegrationTestOnly_1234567890ABCDEFGHIJ";

    private static readonly TokenValidationParameters Parameters = new()
    {
        ValidateIssuer = true,
        ValidIssuer = "CulinaryBlog",
        ValidateAudience = true,
        ValidAudience = "CulinaryBlogApp",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    private static string MintToken(string role, bool valid)
    {
        var keyBytes = Encoding.UTF8.GetBytes(Key);
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(valid ? 30 : -5);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, role)
        };
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "CulinaryBlog",
            audience: "CulinaryBlogApp",
            claims: claims,
            expires: expires,
            signingCredentials: signingCredentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public void Authorize_WhenAuthorizationHeaderIsValidAdminBearer_ShouldAllow()
    {
        var httpContext = new DefaultHttpContext(); httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();
        httpContext.Request.Path = "/hangfire";
        httpContext.Request.Headers.Authorization = "Bearer " + MintToken("Admin", valid: true);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", Key },
                { "Jwt:Issuer", "CulinaryBlog" },
                { "Jwt:Audience", "CulinaryBlogApp" }
            })
            .Build();
        var filter = new HangfireDashboardAuthorizationFilter(configuration);
        var context = new AspNetCoreDashboardContext(Mock.Of<JobStorage>(), new DashboardOptions(), httpContext);
        filter.Authorize(context).Should().BeTrue();
    }

    [Fact]
    public void Authorize_WhenNoAuthorizationHeader_ShouldDeny()
    {
        var httpContext = new DefaultHttpContext(); httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();
        httpContext.Request.Path = "/hangfire";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", Key },
                { "Jwt:Issuer", "CulinaryBlog" },
                { "Jwt:Audience", "CulinaryBlogApp" }
            })
            .Build();
        var filter = new HangfireDashboardAuthorizationFilter(configuration);
        var context = new AspNetCoreDashboardContext(Mock.Of<JobStorage>(), new DashboardOptions(), httpContext);
        filter.Authorize(context).Should().BeFalse();
    }

    [Fact]
    public void Authorize_WhenRoleIsAuthor_ShouldDeny()
    {
        var httpContext = new DefaultHttpContext(); httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();
        httpContext.Request.Path = "/hangfire";
        httpContext.Request.Headers.Authorization = "Bearer " + MintToken("Author", valid: true);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", Key },
                { "Jwt:Issuer", "CulinaryBlog" },
                { "Jwt:Audience", "CulinaryBlogApp" }
            })
            .Build();
        var filter = new HangfireDashboardAuthorizationFilter(configuration);
        var context = new AspNetCoreDashboardContext(Mock.Of<JobStorage>(), new DashboardOptions(), httpContext);
        filter.Authorize(context).Should().BeFalse();
    }

    [Fact]
    public void Authorize_WhenTokenIsExpired_ShouldDeny()
    {
        var httpContext = new DefaultHttpContext(); httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();
        httpContext.Request.Path = "/hangfire";
        httpContext.Request.Headers.Authorization = "Bearer " + MintToken("Admin", valid: false);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", Key },
                { "Jwt:Issuer", "CulinaryBlog" },
                { "Jwt:Audience", "CulinaryBlogApp" }
            })
            .Build();
        var filter = new HangfireDashboardAuthorizationFilter(configuration);
        var context = new AspNetCoreDashboardContext(Mock.Of<JobStorage>(), new DashboardOptions(), httpContext);
        filter.Authorize(context).Should().BeFalse();
    }

    [Fact]
    public void Authorize_WhenValidAdminTokenIsInQueryString_ShouldDeny()
    {
        var httpContext = new DefaultHttpContext(); httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();
        httpContext.Request.Path = "/hangfire";
        httpContext.Request.QueryString = new QueryString("?access_token=" + MintToken("Admin", valid: true));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", Key },
                { "Jwt:Issuer", "CulinaryBlog" },
                { "Jwt:Audience", "CulinaryBlogApp" }
            })
            .Build();
        var filter = new HangfireDashboardAuthorizationFilter(configuration);
        var context = new AspNetCoreDashboardContext(Mock.Of<JobStorage>(), new DashboardOptions(), httpContext);
        filter.Authorize(context).Should().BeFalse();
    }
}
