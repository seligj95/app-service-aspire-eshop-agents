---
page_type: sample
languages:
- azdeveloper
- dotnet
- bicep
- html
- csharp
products:
- azure
- azure-app-service
- azure-ai-studio
- azure-openai
urlFragment: fashion-assistant-ai-agent-web-app
name: Fashion Assistant Web App with Azure AI Agents
description: A .NET Blazor web application that demonstrates how to integrate Azure AI Agents for a fashion e-commerce experience with inventory management and shopping cart functionality.
---
<!-- YAML front-matter schema: https://review.learn.microsoft.com/en-us/help/contribute/samples/process/onboarding?branch=main#supported-metadata-fields-for-readmemd -->

# Fashion Assistant Web App with Azure AI Agents

This sample contains a .NET Blazor web application that uses Azure AI Agents to create an interactive fashion shopping experience. The application demonstrates how to connect a web interface to Azure AI Agents for product recommendations, shopping assistance, and inventory management.

## Features

- Interactive Blazor UI for fashion e-commerce
- Integration with Azure AI Agents for intelligent shopping assistance
- Inventory management with product catalog
- Shopping cart functionality
- Secure authentication with Azure Identity

## Architecture

The application consists of:

- A .NET 8 Blazor web application frontend 
- Azure AI Agent integration for intelligent shopping assistance
- RESTful APIs for inventory and cart management
- Azure App Service deployment infrastructure using Bicep

## Prerequisites

- .NET 8 SDK
- Azure subscription
- Azure AI Studio project with a configured Agent
- Visual Studio 2022 or Visual Studio Code

## Getting Started

1. Clone this repository
2. Configure your Azure AI Agent connection string and Agent ID in `appsettings.json` or as environment variables
3. Run the application locally:
   ```
   cd src/webapp
   dotnet run
   ```
4. Deploy to Azure App Service using the provided Bicep templates in the `infra` directory

## Configuration

The application requires the following configuration:

```json
"AzureAIAgent": {
  "ConnectionString": "<your-azure-ai-agent-connection-string>",
  "AgentId": "<your-azure-ai-agent-id>"
}
```

These can also be provided as environment variables:
- `AzureAIAgent__ConnectionString`
- `AzureAIAgent__AgentId`

## Deployment

This sample includes Bicep templates for infrastructure-as-code deployment to Azure. See the `infra` directory for details.

## Contributing

This project welcomes contributions and suggestions. See the CONTRIBUTING.md file for more information.

## License

This project is licensed under the MIT License - see the LICENSE.md file for details.
