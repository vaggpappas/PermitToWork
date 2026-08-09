// Microsoft.OpenApi 2.0 (shipped with Swashbuckle 10.x) flattened Microsoft.OpenApi.Models
// into Microsoft.OpenApi. Older tutorials still show the .Models namespace — it is gone.
using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using PermitToWork.Api.Authentication;
using PermitToWork.Api.ExceptionHandling;
using PermitToWork.Application;
using PermitToWork.Application.Abstractions;
using PermitToWork.Infrastructure;
using PermitToWork.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddControllers()
    // Enums travel as their names. "Suspended" survives a redeploy that inserts a new
    // enum member; the number 2 quietly starts meaning something else.
    .AddJsonOptions(json => json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Permit To Work API",
        Version = "v1",
        Description = "Employee, team and permit-to-work management."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Paste the token from /api/auth/login. Swagger adds the \"Bearer \" prefix itself."
    });

    // Swashbuckle v10 takes a factory rather than an object, and the scheme is referenced
    // through OpenApiSecuritySchemeReference — OpenApiSecurityScheme.Reference is gone.
    // The scope list must be empty for anything that is not OAuth2.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

const string AngularDevClient = nameof(AngularDevClient);
builder.Services.AddCors(options =>
    options.AddPolicy(AngularDevClient, policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

// First in the pipeline: it can only catch what is thrown after it.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Permit To Work API v1"));
    app.UseCors(AngularDevClient);

    // The API has nothing to serve at the root. In development, send it to the docs
    // rather than a bare 404.
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

    // Idempotent: every step checks before it writes, so this is safe on every start.
    await DatabaseSeeder.SeedAsync(app.Services);
}

app.UseHttpsRedirection();

// Order matters and is not interchangeable: authentication works out who you are,
// authorisation then decides what you may do. Swapped, every [Authorize] endpoint 401s.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>Exposed so integration tests can reference the API host via WebApplicationFactory.</summary>
public partial class Program;
