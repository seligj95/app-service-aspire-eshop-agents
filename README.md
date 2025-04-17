# Fashion Assistant Web App on Azure App Service with Azure AI Agent and OpenAPI Specified Tool

This sample contains a .NET Blazor web application that uses Azure AI Agents to create an interactive fashion shopping experience. The application demonstrates how to connect a web app to an Azure AI Agent from the [Azure AI Agent Service](https://learn.microsoft.com/azure/ai-services/agents/overview). The agent is given the OpenAPI spec for the web app so that it can handle product recommendations, shopping assistance, and shopping cart management, and more on your behalf via a chat interface. This sample builds off of the guidance documented by the AI Agent Service in [How to use Azure AI Agent Service with OpenAPI Specified Tools](https://learn.microsoft.com/azure/ai-services/agents/how-to/tools/openapi-spec?tabs=python&pivots=overview).

## Features

- Interactive Blazor UI for fashion e-commerce
- Integration with Azure AI Agent Service for intelligent shopping assistance
- Sample usage of the OpenAPI Specified Tool with Azure App Service
- Secure authentication to Azure AI Agent Service with Azure managed identity

## Architecture

The application consists of:

- A .NET 8 Blazor web application frontend 
- Azure AI Agent integration for intelligent shopping assistance
- RESTful APIs for shopping cart management
- Azure App Service deployment infrastructure using Bicep and azd template

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Developer CLI](https://aka.ms/azd)
- Azure subscription
- Visual Studio 2022 or Visual Studio Code

## Deploy App Service Resources

1. Clone this repository
2. Login to Azure:

   ```bash
   azd auth login
   ```

3. Initialize your environment:

   ```bash
   azd env new
   ```

4. Deploy the application:

   ```bash
   azd up
   ```

   This will:
   - Build the .NET application
   - Provision Azure resources defined in the Bicep templates
   - Deploy the application to Azure App Service

At this point, once deployment is complete, you can browse to your app and see the general functionality. Feel free to check out the inventory and add some items to the cart. If you try the chat interface at this point, it will not work and it will prompt you to add the environment variables for the AI Agent.

## Create the AI Agent Resources

These resouces must be created in the same subscription as the App Service you created. This is because the managed identity used to access the agent from the App Service only has subsciption level permissions.

1. Sign in to the [Azure portal](https://portal.azure.com).

2. Go to **Create a resource** and search for **AI Agent Service**. Select **AI Agent Service** from the search results, then click **Create**.

3. Fill in the required information:
   - **Subscription**: Select your Azure subscription
   - **Resource group**: Create a new resource group or select an existing one
   - **Region**: Choose a region where the service is available
   - **Name**: Give your AI Agent a unique name
   - **Pricing tier**: Select the appropriate pricing tier for your needs

4. Click **Review + create**, verify the information, then click **Create**.

5. Once deployment is complete, go to your AI Agent Service resource.

6. From the left menu, select **Agents** and click **+ Create** to create a new agent.

7. Enter a name for your agent and select an AI model (GPT-4 recommended for best results).

8. For the agent instructions, use the following:
   ```
   You are an agent for a fashion store that sells clothing. You have the ability to view inventory, update the customer's shopping cart, and answer questions about the clothing items that are in the inventory. You should not answer questions about topics that are unrelated to the fashion store. If a user asks an unrelated question, please respond by telling them you that you can only talk about things that are related to the fashion store.
   ```

9. Click **Next**, add any additional details if needed, then click **Create**.

10. Make note of the **Agent ID** as you'll need this later when configuring the web application.

## Add the OpenAPI Specified Tool to the AI Agent

1. In the Azure portal, navigate to your AI Agent Service resource.

2. Select the agent you created in the previous step.

3. From the left menu, select **Tools** and click **+ Add**.

4. Select **OpenAPI Specified** as the tool type.

5. Name your tool (for example, "FashionStoreInventoryAPI").

6. For the OpenAPI specification, you have two options:
   - **URL**: After deploying your App Service, use the URL of your swagger.json file (`https://{your-app-service-url}/swagger/v1/swagger.json`)
   - **File Upload**: You can upload the swagger.json file from your project

7. If your API requires authentication, configure the appropriate authentication method.

8. Click **Add** to add the tool to your agent.

9. Make note of the **Connection String** for your AI Agent Service as you'll need this for the web application configuration.

## Update App Service Environment Variables

After setting up the AI Agent and adding the OpenAPI Specified Tool, you need to configure your App Service with the appropriate environment variables:

1. In the Azure portal, navigate to your deployed App Service.

2. From the left menu, select **Configuration**.

3. In the **Application settings** tab, click **+ New application setting** and add the following settings:
   - **Name**: `AzureAIAgent__ConnectionString`
   - **Value**: The connection string you noted from your AI Agent Service

4. Add another application setting:
   - **Name**: `AzureAIAgent__AgentId`
   - **Value**: The Agent ID you noted when creating your agent

5. Click **Save** at the top of the page and confirm when prompted.

6. The app will restart with the new settings applied.

## Use the app

Now that all of the support resources are created and updated, the app is ready for use. Ask the agent questions such as:

- What's in my cart?
- Add a small denim jacket to my cart
- Do we have any blazers in stock?

You can also ask general questions about the items and the agent should be able to provide information.

- Tell me about Red Slim Fit Checked Casual Shirt
- Is the blazer warm?

## Clean-up

When you're done with this app, run the following to delete the App Service resources:

   ```bash
   azd down
   ```

You also need to delete the AI Agent Service and associated resources. If you created all of these resources in a single resource group, deleting that resource group will delete these resources.

## Troubleshooting

### Common Issues

1. **Chat Not Working**
   - Verify that the environment variables (`AzureAIAgent__ConnectionString` and `AzureAIAgent__AgentId`) are correctly set in the App Service configuration.
   - Check that the AI Agent was properly created and configured with the correct OpenAPI tool.
   - Ensure the OpenAPI specification URL is accessible from the Azure AI Agent Service.
   - Ensure the App Service URL is updated in the `swaggger.json` provided to the OpenAPI Specified Tool.

2. **Permission Issues**
   - If you encounter authentication errors, ensure that your App Service's managed identity has proper permissions to access the Azure AI Agent Service. The managed identity needs at least the `Microsoft.MachineLearningServices/workspaces/agents/action` permission to interact with the Agent. The provided Azure AI Developer role has this permission and should be sufficient. If you decide to change this role, be sure it has the necessary permission.

3. **API Issues**
   - If the agent is unable to perform actions on the inventory or cart, check the API routes in the OpenAPI specification.
   - Verify that the API endpoints are responding correctly by testing them directly in the Swagger UI at `/api/docs`.

### Viewing Logs

To view logs for your App Service:

1. Navigate to your App Service in the Azure portal.
2. In the left menu, select **Monitoring** > **Log stream** to view real-time logs.
3. These logs will reveal any application issues that you may need to address.

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

- The application uses Azure managed identities for secure authentication to Azure AI Agent Service in production environments.
- For local development, DefaultAzureCredential is used, which tries multiple authentication methods.
- No sensitive credentials are stored in the code.

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.
