var builder = DistributedApplication.CreateBuilder(args);

// Add the webapp project
var webapp = builder.AddProject<Projects.dotnetfashionassistant>("webapp")
    .WithExternalHttpEndpoints()
    .PublishAsAzureAppServiceWebsite((infrastructure, app) =>
    {
        // Configure the App Service WebSite resource here if needed
        // The default configuration should work for most scenarios
    });

builder.Build().Run();
