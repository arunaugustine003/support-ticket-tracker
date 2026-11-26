using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SupportTracker.Security;

internal sealed class TicketSecurity
{
    public const string CookieName = "__Host-supportdesk";
    public const string CsrfHeader = "X-SupportDesk-CSRF";

    public static CookieOptions SessionCookie() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        IsEssential = true
    };

    private const string Issuer = "SupportTracker";
    private const string Audience = "SupportTracker.CookieApi";
    private readonly string username;
    private readonly byte[]? passwordHash;
    private readonly SymmetricSecurityKey key;

    public TicketSecurity(IConfiguration configuration)
    {
        username = configuration["Auth:Username"] ?? configuration["DemoAccess:Username"] ?? "admin";
        var password = configuration["Auth:Password"] ?? configuration["DemoAccess:Password"];
        if (!string.IsNullOrWhiteSpace(password))
            passwordHash = Hash(password);

        var signingKey = configuration["Auth:SigningKey"];
        var keyBytes = string.IsNullOrWhiteSpace(signingKey)
            ? RandomNumberGenerator.GetBytes(32)
            : Encoding.UTF8.GetBytes(signingKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Auth:SigningKey must be at least 32 bytes.");

        key = new SymmetricSecurityKey(keyBytes);
    }

    public bool CheckCredentials(string suppliedUsername, string suppliedPassword) =>
        passwordHash is not null
        && (CryptographicOperations.FixedTimeEquals(Hash(suppliedUsername), Hash(username))
            & CryptographicOperations.FixedTimeEquals(Hash(suppliedPassword), passwordHash));

    public (string AccessToken, DateTime ExpiresAt) IssueToken()
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(15);
        var jwt = new JwtSecurityToken(
            Issuer, Audience,
            [new(JwtRegisteredClaimNames.Sub, username)],
            now, expiresAt, new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt);
    }

    public void ConfigureJwt(JwtBearerOptions options)
    {
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Sub
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(CookieName, out var token) && !string.IsNullOrEmpty(token))
                    context.Token = token;
                else
                    context.NoResult();

                return Task.CompletedTask;
            }
        };
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}

internal static class TicketSecurityRegistration
{
    public static void AddTicketSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var security = new TicketSecurity(configuration);
        services.AddSingleton(security);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(security.ConfigureJwt);
        services.AddAuthorization();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Support Desk API",
                Version = "v1",
                Description = "Sign in to Support Desk in this browser or run POST /api/auth/login below. Swagger uses the authentication cookie automatically and adds the required header to writes."
            });
            options.AddSecurityDefinition("SessionCookie", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Cookie,
                Name = TicketSecurity.CookieName,
                Description = "The browser sets this HttpOnly cookie after login."
            });
            options.OperationFilter<SessionOperationFilter>();
        });
    }
}

internal sealed class SessionOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Responses.TryAdd("429", new OpenApiResponse { Description = "Rate limit exceeded" });
        if (context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            return;

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "SessionCookie" }
            }] = Array.Empty<string>()
        });
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Authentication required" });
    }
}
