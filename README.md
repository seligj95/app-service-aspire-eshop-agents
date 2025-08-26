# Fashion Assistant Web App with .NET Aspire and Azure AI Foundry

This sample demonstrates a modern cloud-native .NET Blazor web application enhanced with [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview) orchestration and Azure AI Foundry. The application showcases how to build an interactive fashion shopping experience that combines local development productivity with cloud-native observability and Azure AI integration.

The application demonstrates:
- **.NET Aspire Integration**: Enhanced development experience with service discovery, telemetry, and health checks
- **Azure AI Foundry Integration**: Intelligent shopping assistance using [Azure AI Foundry](https://learn.microsoft.com/azure/ai-studio/what-is-ai-studio)
- **OpenAPI Tool Integration**: How to connect AI agents to web APIs using OpenAPI specifications
- **Modern Cloud-Native Architecture**: Combining Aspire's local development benefits with Azure App Service deployment

This sample builds upon the guidance from [How to use Azure AI Foundry with OpenAPI Specified Tools](https://learn.microsoft.com/azure/ai-studio/how-to/tools/openapi-spec) while adding modern .NET development practices.

## Features

- **Interactive Blazor UI** for fashion e-commerce with real-time updates
- **Azure AI Foundry integration** for intelligent shopping assistance with dynamic agent creation
- **.NET Aspire orchestration** with enhanced observability and service discovery
- **Automated app settings configuration** for seamless deployment
- **Health check endpoints** (`/health`, `/alive`) for production monitoring
- **OpenTelemetry integration** for comprehensive telemetry and logging
- **Sample OpenAPI Specified Tool** implementation with Azure App Service
- **Secure authentication** to Azure AI Foundry with Azure managed identity and endpoint-based configuration

## Architecture

The application consists of:

- **AppHost Project**: .NET Aspire orchestration for local development and Azure deployment
- **ServiceDefaults Project**: Shared Aspire service configuration (health checks, telemetry, service discovery)
- **Web Application**: .NET 9 Blazor frontend with AI agent integration
- **Azure Infrastructure**: App Service and Azure AI Foundry resources using Bicep templates
- **AI Agent Integration**: Intelligent shopping assistance with automated agent creation and OpenAPI tool integration

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure Developer CLI](https://aka.ms/azd)
- Azure subscription
- Visual Studio 2022 or Visual Studio Code

## Quick Start

**Before running this application, you'll need to deploy the inventory service:**

1. **Deploy the Inventory API**:
   ```bash
   git clone https://github.com/seligj95/app-service-python-mcp-inventory.git
   cd app-service-python-mcp-inventory
   azd up
   ```
   📝 **Save the deployed URL** - you'll need it for configuration.

2. **Deploy this Fashion Assistant App**:
   ```bash
   git clone <this-repository>
   cd ai-agent-openai-web-app
   azd up
   ```

3. **Configure the Inventory URL**:
   - Go to Azure Portal → Your App Service → Configuration
   - Update `EXTERNAL_INVENTORY_API_URL` with your inventory service URL
   - Save and restart the app

Your fashion assistant with real-time inventory is now ready! 🎉

## Local Development with Aspire

For the best development experience, use the .NET Aspire AppHost to run the application locally:

1. **Clone this repository**
2. **Install Aspire workload** (if not already installed):
   ```bash
   dotnet workload update
   dotnet workload install aspire
   ```
3. **Configure the external inventory API** (required for inventory functionality):
   
   Create a `.env` file in the root directory with the following content:
   ```env
   EXTERNAL_INVENTORY_API_URL=https://your-inventory-api.azurewebsites.net
   ```
   
   Replace `https://your-inventory-api.azurewebsites.net` with the actual URL of your external inventory API.

4. **Run the AppHost project**:
   ```bash
   cd src/ai-agent-openai-web-app.AppHost
   dotnet run
   ```

This will:
- Launch the **Aspire Dashboard** (typically at `https://localhost:17071`)
- Start the web application with enhanced telemetry and health checks
- Provide real-time monitoring, logging, and service discovery
- Enable hot reload and enhanced debugging capabilities
- Connect to your external inventory API for real-time product data

### Environment Variables for Local Development

The following environment variables can be configured in your `.env` file:

| Variable | Description | Required | Example |
|----------|-------------|----------|---------|
| `EXTERNAL_INVENTORY_API_URL` | URL of your external inventory API | Yes | `https://my-inventory.azurewebsites.net` |

**Note**: Without the `EXTERNAL_INVENTORY_API_URL` environment variable, the inventory page will show a loading state followed by an empty inventory message.

### Inventory API Requirements

This application requires an external inventory API to provide product data. We recommend deploying the provided inventory service:

**📦 Deploy the Inventory Service:**
1. **Clone the inventory repository**:
   ```bash
   git clone https://github.com/seligj95/app-service-python-mcp-inventory.git
   cd app-service-python-mcp-inventory
   ```

2. **Deploy to Azure**:
   ```bash
   azd up
   ```

3. **Note the deployed URL** (e.g., `https://your-inventory-app.azurewebsites.net`) - you'll need this for configuration.

**API Format:**
The inventory API exposes an endpoint at `/api/inventory` that returns JSON in the following format:

```json
[
  {
    "id": 1,
    "name": "Product Name",
    "category": "Category",
    "price": 29.99,
    "description": "Product description",
    "sizes": {
      "XS": 5,
      "S": 10,
      "M": 15,
      "L": 8,
      "XL": 3,
      "XXL": 0,
      "XXXL": 2
    }
  }
]
```

The application will automatically map this external format to the internal inventory structure.

The Aspire dashboard provides:
- **Real-time logs** from all services
- **Distributed tracing** across service calls
- **Metrics and performance data**
- **Health check status** monitoring
- **Service topology** visualization

## Deploy to Azure

Deploy the Aspire-enhanced application to Azure App Service:

1. **Login to Azure**:
   ```bash
   azd auth login
   ```

2. **Initialize your environment**:
   ```bash
   azd env new
   ```

3. **Deploy the application**:
   ```bash
   azd up
   ```

   This will:
   - Build the .NET 9 application with Aspire enhancements
   - Provision Azure AI Foundry resources defined in the Bicep templates
   - Deploy the application to Azure App Service with health check endpoints
   - Configure managed identity for secure AI Foundry access
   - Automatically configure app settings for agent connectivity
   - Set the `EXTERNAL_INVENTORY_API_URL` to a placeholder value that **must be manually updated**

### Post-Deployment Configuration

**Important**: After deployment, you must manually configure the external inventory API URL:

1. **Navigate to your App Service** in the Azure Portal
2. **Go to Configuration** → Application Settings
3. **Find the `EXTERNAL_INVENTORY_API_URL` setting** (it will be set to `https://<YOUR-INVENTORY-APP-NAME>.azurewebsites.net`)
4. **Update the value** to your actual inventory API URL (e.g., `https://my-inventory-api.azurewebsites.net`)
5. **Save the changes** and restart the App Service

Until you complete this configuration, the inventory page will display a message asking you to configure the app setting.

The deployment process takes approximately 5-10 minutes. Once complete and properly configured, your application will include:
- **Enhanced telemetry** and monitoring capabilities
- **Health check endpoints** at `/health` and `/alive`
- **Production-ready logging** and metrics
- **Secure AI Foundry integration** via managed identity and endpoint configuration
- **Automated agent creation** and configuration

## AI Agent Configuration (Optional)

The application now includes **automated agent creation** that works out of the box. However, if you want to customize the agent or create additional agents, you can use the Azure AI Foundry portal:

### Access Azure AI Foundry

1. Sign in to the [Azure portal](https://portal.azure.com) and go to the resource group that was created by the azd template.
2. Click the **Azure AI project** resource that was created for you.
3. Click **Launch studio** to open the Azure AI Foundry studio.

### View or Customize Your Agent

1. On the left-hand side under "Build and customize", select **Agents**.
2. You should see the automatically created agent named "Fashion Assistant".
3. Click on the agent to view or modify its configuration.

The default agent includes:
- **Optimized instructions** for fashion store assistance
- **Pre-configured OpenAPI tool** connected to your App Service
- **Automatic endpoint configuration** using your deployed application URL

### Manual Agent Creation (Advanced)

If you prefer to create agents manually or need multiple agents:

1. In Azure AI Foundry, click **+ New agent**.
2. Add the following instructions:

  ```
  You are an agent for a fashion store that sells clothing. You have the ability to view inventory, update the customer's shopping cart, and answer questions about the clothing items that are in the inventory. You should not answer questions about topics that are unrelated to the fashion store. If a user asks an unrelated question, please respond by telling them that you can only talk about things that are related to the fashion store.
  ```

3. Add the OpenAPI tool by clicking **+ Add** next to **Action**.
4. Select **OpenAPI 3.0 specified tool**.
5. Use the description: "This tool is used to interact with and manage an online fashion store. The tool can add or remove items from a shopping cart as well as view inventory."
6. Copy the OpenAPI specification from [swagger.json](./src/webapp/swagger.json) and update the server URL to your deployed App Service URL.

### Environment Variables (Automated)

The following environment variables are automatically configured during deployment:
- `AI_PROJECT_ENDPOINT`: Your Azure AI Foundry project endpoint
- `AI_SERVICES_ENDPOINT`: Your Azure AI Services endpoint  
- `AI_MODEL_DEPLOYMENT_NAME`: The deployed model name (gpt-4o-mini)

These settings enable the application to automatically create and manage agents as needed.

## Use the app

The application is now ready to use immediately after deployment! The AI agent is automatically created and configured when you first interact with the chat interface. Ask the agent questions such as:

- What's in my cart?
- Add a small denim jacket to my cart
- Do we have any blazers in stock?

You can also ask general questions about the items and the agent should be able to provide information:

- Tell me about Red Slim Fit Checked Casual Shirt
- Is the blazer warm?

The agent will automatically create itself on first use and connect to your application's API using the deployed OpenAPI specification.

## Clean-up

When you're done with this app, run the following to delete all Azure resources:

```bash
azd down
```

## Development Benefits with .NET Aspire

This application showcases the benefits of .NET Aspire for modern cloud-native development:

### **Local Development**
- **Unified Dashboard**: Single view of all services, logs, and telemetry
- **Service Discovery**: Automatic service-to-service communication
- **Hot Reload**: Fast development iterations with immediate feedback
- **Health Monitoring**: Real-time health status of all components

### **Production Deployment**
- **Enhanced Observability**: OpenTelemetry integration for comprehensive monitoring
- **Health Endpoints**: Built-in `/health` and `/alive` endpoints for Azure monitoring
- **Structured Logging**: Improved log correlation and debugging
- **Cloud-Native Patterns**: Service discovery, circuit breakers, and retry policies

### **Azure Integration**
- **Seamless Deployment**: Aspire configuration translates directly to Azure App Service
- **Managed Identity**: Secure authentication without connection strings
- **Infrastructure as Code**: Bicep templates work alongside Aspire orchestration
- **Automated Configuration**: Environment variables and agent setup handled automatically

## Troubleshooting

### Common Issues

1. **Inventory Not Loading**
   - **Missing Inventory Service**: Ensure you've deployed the inventory service from https://github.com/seligj95/app-service-python-mcp-inventory
   - **Local Development**: Ensure you have created a `.env` file with the correct `EXTERNAL_INVENTORY_API_URL` pointing to your deployed inventory service
   - **Azure Deployment**: Check that you've updated the `EXTERNAL_INVENTORY_API_URL` app setting from the placeholder to your actual inventory API URL
   - **API Issues**: Verify your inventory API is accessible and returns data in the expected format at `/api/inventory`
   - **Network Issues**: Ensure there are no CORS or firewall restrictions blocking access to your inventory API

2. **Chat Not Working**
   - The agent is now created automatically on first use. If you still experience issues:
   - Check the application logs in Azure App Service for any agent creation errors.
   - Verify that the Azure AI Foundry resources are properly provisioned.
   - Ensure the managed identity has proper permissions to access Azure AI Foundry.

3. **Permission Issues**
   - If you encounter authentication errors, ensure that your App Service's managed identity has proper permissions to access Azure AI Foundry. The managed identity needs the appropriate cognitive services permissions.

4. **API Issues**
   - If the agent is unable to perform actions on the inventory or cart, the OpenAPI tool should be automatically configured.
   - Verify that the API endpoints are responding correctly by testing them directly in the Swagger UI at `/api/docs`.

5. **Aspire Development Issues**
   - If the Aspire dashboard doesn't load, ensure you have the latest .NET 9 SDK and Aspire workload installed.
   - Check that all project references are correctly configured between AppHost, ServiceDefaults, and the web application.
   - Verify that the required Aspire NuGet packages are installed and up to date.

### Viewing Logs

**Local Development:**
- Use the Aspire Dashboard at `https://localhost:17071` for real-time logs and telemetry
- View structured logs with correlation IDs across all services
- Monitor health checks and service dependencies

**Production (Azure):**
1. Navigate to your App Service in the Azure portal
2. In the left menu, select **Monitoring** > **Log stream** for real-time logs
3. Use **Application Insights** for enhanced telemetry and distributed tracing
4. Check **Health checks** at `https://your-app.azurewebsites.net/health`

### Health Check Endpoints

The application includes Aspire-enhanced health check endpoints:
- **`/health`**: Comprehensive health check (all registered health checks must pass)
- **`/alive`**: Basic liveness check (minimal health verification)

These endpoints are automatically configured and provide detailed health status information.

## Understanding the API Capabilities

The OpenAPI specification provides the AI agent with information about available endpoints:

### Cart API Endpoints
- **GET /api/Cart**: Retrieves the current shopping cart contents and total cost
- **DELETE /api/Cart**: Clears all items from the cart
- **POST /api/Cart/add**: Adds an item to the shopping cart
- **PUT /api/Cart/{productId}/size/{size}**: Updates the quantity of a specific item
- **DELETE /api/Cart/{productId}/size/{size}**: Removes a specific item from the cart

### Inventory API Endpoints
- **GET /api/Inventory**: Lists all available inventory items
- **GET /api/Inventory/{id}**: Gets details about a specific product
- **GET /api/Inventory/{id}/size/{size}**: Checks inventory for a specific product size
- **GET /api/Inventory/sizes**: Gets all available sizes in the inventory

## Advanced Agent Interactions

Beyond basic interactions, the AI agent can handle more complex scenarios:

- **Personalized Recommendations**: "I need a business casual outfit for a meeting"
- **Size Guidance**: "What size blazer would fit someone who's 6'2" and 180 pounds?"
- **Outfit Coordination**: "What would go well with the black denim jacket?"
- **Shopping Cart Management**: "Remove the large shirt and add a medium instead"
- **Inventory Checks**: "Do you have any red shirts in medium?"
- **Price Inquiries**: "What's the price range for blazers?"

## Security Considerations

- The application uses **Azure managed identities** for secure authentication to Azure AI Foundry
- **No sensitive credentials** are stored in the code or configuration
- **Aspire ServiceDefaults** provide secure defaults for service-to-service communication
- **Health check endpoints** are configured with appropriate security considerations for production
- **Endpoint-based authentication** eliminates the need for connection strings
- **Automatic agent management** reduces security surface area by eliminating manual configuration

## Project Structure

```
├── src/
│   ├── ai-agent-openai-web-app.AppHost/          # Aspire orchestration
│   │   ├── AppHost.cs                             # Main orchestration logic
│   │   └── appsettings.json                       # AppHost configuration
│   ├── ai-agent-openai-web-app.ServiceDefaults/  # Shared Aspire services
│   │   └── Extensions.cs                          # Health checks, telemetry, service discovery
│   └── webapp/                                    # Main web application
│       ├── Program.cs                             # App startup with Aspire integration
│       ├── Components/                            # Blazor components
│       ├── Controllers/                           # API controllers
│       ├── Services/                              # AI agent service
│       └── swagger.json                           # OpenAPI specification
├── infra/                                         # Azure infrastructure
│   ├── main.bicep                                 # Main Bicep template
│   └── core/                                      # Reusable Bicep modules
└── azure.yaml                                     # Azure Developer CLI configuration
```

## Learn More

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Azure AI Foundry Documentation](https://learn.microsoft.com/azure/ai-studio/)
- [OpenAPI Specified Tools](https://learn.microsoft.com/azure/ai-studio/how-to/tools/openapi-spec)
- [Azure App Service](https://learn.microsoft.com/azure/app-service/)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/)

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.