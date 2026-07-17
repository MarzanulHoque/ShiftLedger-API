using ShiftLedger.Application;
using ShiftLedger.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------

// Controllers (attribute-routed). Minimal APIs are intentionally NOT used — the
// project standardises on controllers for the large endpoint map (see docs/02 §7).
builder.Services.AddControllers();

// Swagger / OpenAPI. Swagger is the authoritative API contract, served at /swagger
// in Development (see docs/04_API_Specification.md).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Clean Architecture layer wiring — each layer owns its own registrations.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// ---------------------------------------------------------------------------
// HTTP request pipeline
// ---------------------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed so the integration-test project can bootstrap the API through
// WebApplicationFactory<Program> (wired from phase P1).
public partial class Program { }
