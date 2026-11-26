using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SupportTracker.Data;
using SupportTracker.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)));
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddTicketSecurity(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);

        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "Wait a moment and try again."
        }, cancellationToken);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
            return RateLimitPartition.GetNoLimiter("static");

        var identity = context.User.Identity?.Name;
        var partition = identity is null ? $"ip:{context.Connection.RemoteIpAddress}" : $"user:{identity}";
        return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1)
        });
    });
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
});
builder.Services.AddDbContext<TicketContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("Tickets")));
var app = builder.Build();
// Apply migrations at startup; SQL Server can take time to become ready in Compose.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TicketContext>();
    for (var attempt = 1; ; attempt++)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch (Exception ex) when (attempt < 12)
        {
            app.Logger.LogWarning(ex, "Database is not ready (attempt {Attempt}/12)", attempt);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
        context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseSwagger();
app.UseSwaggerUI(options => options.UseRequestInterceptor("""
    (request) => {
      const method = (request.method || "GET").toUpperCase();
      if (new URL(request.url, window.location.href).origin === window.location.origin
          && !["GET", "HEAD", "OPTIONS"].includes(method)) {
        request.headers["X-SupportDesk-CSRF"] = "1";
      }
      return request;
    }
    """));
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
// A cross-origin page cannot send this custom header without a successful CORS
// preflight. The API serves same-origin clients and grants no CORS permissions.
app.Use(async (context, next) =>
{
    var request = context.Request;
    if (request.Path.StartsWithSegments("/api")
        && !HttpMethods.IsGet(request.Method)
        && !HttpMethods.IsHead(request.Method)
        && !HttpMethods.IsOptions(request.Method)
        && request.Headers[TicketSecurity.CsrfHeader] != "1")
    {
        await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Missing or invalid CSRF header.")
            .ExecuteAsync(context);
        return;
    }

    await next();
});
app.MapControllers().RequireAuthorization();
app.MapFallbackToFile("index.html");
app.Run();
public partial class Program { }
