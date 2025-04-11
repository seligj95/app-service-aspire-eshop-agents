using dotnetfashionassistant.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Client for Fashion Assistant API (phi-3 sidecar)
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.Configuration["FashionAssistantAPI:Url"] ?? "http://localhost:8000/predict") });

// Default HttpClient for cart and inventory API calls
builder.Services.AddHttpClient("LocalApi", (serviceProvider, client) =>
{
    // Get the current HttpContext to determine the base URL
    var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
    var request = httpContextAccessor.HttpContext?.Request;
    
    if (request != null)
    {
        // Use the current host as the base address when in production
        var baseUrl = $"{request.Scheme}://{request.Host}/";
        client.BaseAddress = new Uri(baseUrl);
    }
    else
    {
        // Fallback to localhost for development
        client.BaseAddress = new Uri("http://localhost:5256/");
    }
});

builder.Services.AddBlazorBootstrap();

// Register CartUpdateService as a singleton so it can be used for cross-component communication
builder.Services.AddSingleton<dotnetfashionassistant.Services.CartUpdateService>();

// Add HttpContextAccessor to access the current request context
builder.Services.AddHttpContextAccessor();

// Add controllers for API endpoints
builder.Services.AddControllers();

// Register the Swagger generator and define the OpenAPI specification
builder.Services.AddSwaggerGen(c =>
{    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Fashion Store Inventory API",
        Version = "v1",
        Description = "API for managing fashion store inventory"
    });

    // Use XML comments for Swagger documentation
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    
    // Enable XML comments if the file exists
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// Enable Swagger UI in all environments
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fashion Store Inventory API v1");
    c.RoutePrefix = "api/docs";
});

app.UseStaticFiles();
app.UseAntiforgery();

// Map controllers for API endpoints
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
