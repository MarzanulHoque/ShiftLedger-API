using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using ShiftLedger.Api;
using ShiftLedger.Application;
using ShiftLedger.Application.Common.Options;
using ShiftLedger.Infrastructure;
using ShiftLedger.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------

// Controllers (attribute-routed). Minimal APIs are intentionally NOT used — the
// project standardises on controllers for the large endpoint map (see docs/02 §7).
// Serialize/accept enums as their string names (matches the DB string storage and readable payloads).
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Swagger / OpenAPI. Swagger is the authoritative API contract, served at /swagger
// in Development (see docs/04_API_Specification.md).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Clean Architecture layer wiring — each layer owns its own registrations.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// CORS for the Angular dev server (SPA on :4200 calling the API on :5184). In staging/production
// the SPA is served same-origin behind a reverse proxy (docs/10), so this policy is dev-only.
const string SpaCorsPolicy = "SpaDev";
builder.Services.AddCors(options =>
    options.AddPolicy(SpaCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()));

// Problem-details error handling (400 validation, 409 concurrency, 422 business-rule).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// JWT authentication (access tokens; refresh handled by /auth/refresh).
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        };
    });
// Every endpoint requires an authenticated user by default; opt out with [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

var app = builder.Build();

app.UseExceptionHandler();

// ---------------------------------------------------------------------------
// HTTP request pipeline
// ---------------------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(SpaCorsPolicy);
}
else
{
    // Only force HTTPS outside Development; the SPA dev server talks plain HTTP to :5184.
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// In Development also seed demo accounts (admin + two employees) for the quick-login buttons.
await DbSeeder.SeedAsync(app.Services, seedDemoData: app.Environment.IsDevelopment());

app.Run();

// Exposed so the integration-test project can bootstrap the API through
// WebApplicationFactory<Program> (wired from phase P1).
public partial class Program { }
