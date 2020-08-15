---
languages:
- fsharp
products:
- azure
- azure-cosmos-db
- dotnetcore
page_type: sample
description: "This sample shows you how to use the Azure Cosmos DB service to store and access data from an F# .NET Core console application."
---

# Developing an F# .NET Core console app using Azure Cosmos DB
This sample shows you how to use the Azure Cosmos DB service to store and access data from an F# .NET Core console application.

For a complete end-to-end walkthrough of creating this application (in C#), please refer to the [full tutorial on the Azure Cosmos DB documentation page](https://aka.ms/CosmosDotnetGetStarted).

## Running this sample

1. For running this sample, you must have the following prerequisites:
	- An active Azure Cosmos DB account - If you don't have an account, refer to the [Create a database account](https://docs.microsoft.com/azure/cosmos-db/create-sql-api-dotnet#create-a-database-account) article.
	- .NET Core 3.1 or higher and Visual Studio Code installed.

1. Clone this repository using Git for Windows (http://www.git-scm.com/), or download the zip file.

1. Go to the **src/FsCosmosGettingStartedTutorial folder** and open it with Code
2. Update the `Program.fs` file with the URI and key of your Cosmos DB account
   1. Retrieve the URI and PRIMARY KEY (or SECONDARY KEY) values from the Keys blade of your Azure Cosmos DB account in the Azure portal. For more information on obtaining endpoint & keys for your Azure Cosmos DB account refer to [View, copy, and regenerate access keys and passwords](https://docs.microsoft.com/en-us/azure/cosmos-db/manage-account#keys)
   2. Replace these values in the `endpointUri` and `primaryKey` definitions
3. Save the file, open a terminal window, and enter `dotnet run` 

## About the code
The code included in this sample is intended to get you quickly started with an F# .NET Core console application that connects to Azure Cosmos DB, the code strives to be a vis-a-vis implementation of the C# original version.

## More information

- [Azure Cosmos DB Documentation](https://docs.microsoft.com/azure/cosmos-db/index)
- [Azure Cosmos DB .NET SDK for SQL API](https://docs.microsoft.com/azure/cosmos-db/sql-api-sdk-dotnet)
- [Azure Cosmos DB .NET SDK Reference Documentation](https://docs.microsoft.com/dotnet/api/overview/azure/cosmosdb?view=azure-dotnet)
