using DotNetEnv;

var currentDir = Directory.GetCurrentDirectory();
var solutionRoot = currentDir;

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

var webapp = builder.AddProject<Projects.dotnetfashionassistant>("webapp")
    .WithEnvironment("AI_PROJECT_ENDPOINT", Environment.GetEnvironmentVariable("AI_PROJECT_ENDPOINT") ?? "")
    .WithEnvironment("AI_MODEL_DEPLOYMENT_NAME", Environment.GetEnvironmentVariable("AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-4o")
    .WithEnvironment("EXTERNAL_INVENTORY_URL", Environment.GetEnvironmentVariable("EXTERNAL_INVENTORY_URL") ?? "")
    .WithExternalHttpEndpoints()
    .PublishAsAzureAppServiceWebsite((infrastructure, app) =>
    {
    });

builder.Build().Run();
