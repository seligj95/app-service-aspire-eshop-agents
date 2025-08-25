using DotNetEnv;

// Load environment variables from .env file in development
// Find the solution root by looking for the .env file
var currentDir = Directory.GetCurrentDirectory();
var solutionRoot = currentDir;

// Walk up the directory tree to find the .env file
while (solutionRoot != null && !File.Exists(Path.Combine(solutionRoot, ".env")))
{
    var parentDir = Directory.GetParent(solutionRoot);
    solutionRoot = parentDir?.FullName;
}

if (solutionRoot != null && File.Exists(Path.Combine(solutionRoot, ".env")))
{
    var envPath = Path.Combine(solutionRoot, ".env");
    Env.Load(envPath);
    Console.WriteLine($"✅ Loaded .env file from: {envPath}");
}
else
{
    Console.WriteLine("⚠️ WARNING: No .env file found. Environment variables will need to be set manually.");
}

var builder = DistributedApplication.CreateBuilder(args);

// Add the webapp project with environment variables
var webapp = builder.AddProject<Projects.dotnetfashionassistant>("webapp")
    .WithEnvironment("AI_PROJECT_ENDPOINT", Environment.GetEnvironmentVariable("AI_PROJECT_ENDPOINT") ?? "")
    .WithEnvironment("AI_MODEL_DEPLOYMENT_NAME", Environment.GetEnvironmentVariable("AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-4o")
    .WithEnvironment("EXTERNAL_INVENTORY_API_URL", Environment.GetEnvironmentVariable("EXTERNAL_INVENTORY_API_URL") ?? "")
    .WithExternalHttpEndpoints()
    .PublishAsAzureAppServiceWebsite((infrastructure, app) =>
    {
        // Configure the App Service WebSite resource here if needed
        // The default configuration should work for most scenarios
    });

builder.Build().Run();
