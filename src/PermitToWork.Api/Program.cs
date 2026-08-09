// Microsoft.OpenApi 2.0 (shipped with Swashbuckle 10.x) flattened Microsoft.OpenApi.Models
// into Microsoft.OpenApi. Older tutorials still show the .Models namespace — it is gone.
using Microsoft.OpenApi;
using PermitToWork.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Permit To Work API",
        Version = "v1",
        Description = "Employee, team and permit-to-work management."
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Permit To Work API v1"));
    app.UseCors(AngularDevClient);

    // The API has nothing to serve at the root. In development, send it to the docs
    // rather than a bare 404.
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

/// <summary>Exposed so integration tests can reference the API host via WebApplicationFactory.</summary>
public partial class Program;
