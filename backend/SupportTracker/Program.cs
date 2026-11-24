using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportTracker.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)));
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<TicketContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("Tickets")));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(builder.Configuration["FrontendOrigin"] ?? "http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
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
// Optional shared demo login. Production deployments should use a proper identity provider.
var demoPassword = builder.Configuration["DemoAccess:Password"];
if (!string.IsNullOrEmpty(demoPassword))
{
    var expectedUser = builder.Configuration["DemoAccess:Username"] ?? "reviewer";
    app.Use(async (context, next) =>
    {
        var authenticated = false;
        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[6..]));
                var separator = credentials.IndexOf(':');
                if (separator >= 0)
                {
                    static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
                    authenticated = CryptographicOperations.FixedTimeEquals(Hash(credentials[..separator]), Hash(expectedUser))
                        & CryptographicOperations.FixedTimeEquals(Hash(credentials[(separator + 1)..]), Hash(demoPassword));
                }
            }
            catch (FormatException) { }
        }
        if (!authenticated)
        {
            context.Response.Headers.WWWAuthenticate = "Basic realm=\"Support Desk demo\", charset=\"UTF-8\"";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        await next();
    });
}
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();
public partial class Program { }
