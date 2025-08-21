using dotnetfashionassistant.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Default HttpClient for cart and inventory API calls
builder.Services.AddHttpClient("LocalApi", (serviceProvider, client) =>
{
    // Get the current HttpContext to determine the base URL
    var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
    var request = httpContextAccessor.HttpContext?.Request;
    
    // Use the current host as the base address
    var baseUrl = $"{request?.Scheme ?? "https"}://{request?.Host ?? new HostString("localhost")}/";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddBlazorBootstrap();

// Register CartUpdateService as a singleton so it can be used for cross-component communication
builder.Services.AddSingleton<dotnetfashionassistant.Services.CartUpdateService>();

// Register the AzureAIAgentService
builder.Services.AddScoped<dotnetfashionassistant.Services.AzureAIAgentService>();

// Register AgentModeService as a singleton to persist mode state across the application
builder.Services.AddSingleton<dotnetfashionassistant.Services.AgentModeService>();

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
app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseHsts();
app.UseHttpsRedirection();

// Configure Swagger for production use only
app.UseSwagger(c => 
{
    // Dynamically set the server URL based on request
    c.PreSerializeFilters.Add((swaggerDoc, httpReq) => 
    {
        swaggerDoc.Servers = new List<OpenApiServer> 
        { 
            new OpenApiServer { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}" } 
        };
    });
});

// Configure Swagger UI for production
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fashion Store Inventory API v1");
    c.RoutePrefix = "api/docs";
    c.EnableFilter(); // Add filtering capability
    c.DisplayRequestDuration(); // Show request timing info
});

app.UseStaticFiles();
app.UseAntiforgery();

// Map controllers for API endpoints
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map Aspire default endpoints
app.MapDefaultEndpoints();

app.Run();
