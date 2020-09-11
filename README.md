# Cosmos DB SQL API samples with F# and .NET Core 3.1

The purpose of this repository is to offer F# implementations of several samples illustrating how to use the Cosmos DB SQL API for .NET Core, originally written in C#. While I tried to use functional idioms where adequate, I followed the general structure of the original C# examples, so that you can see how C# code maps to F# code. So far, I have got:

* The **Get Started tutorial**, in the src/FsCosmosGettingStartedTutorial folder, a whirlwind tour showing how to do CRUD operations in a Cosmos DB container with its SQL API.
  * You must edit the Program.fs file and put there the end point URL of your Cosmos DB account in the ``endPointUri`` variable and the corresponding authorization key in the ``primaryKey`` variable
  * After saving the Program.fs file you just have have to type ``dotnet run`` and press the ``Enter`` key while inside the folder
  * The detailed original example in C# is [here](https://aka.ms/CosmosDotnetGetStarted).
* The **Queries sample**, in the src/FsQueries folder, showing several ways of doing SQL-like query operations on a Cosmos DB container.
  * You must edit the appSettings.json file and put there the end point URL of your Cosmos DB account and the corresponding authorization key
  * After saving the appSettings.json file you just have have to type ``dotnet run`` and press the ``Enter`` key while inside the folder
  * The original example in C# is [here](https://github.com/Azure/azure-cosmos-dotnet-v3/tree/master/Microsoft.Azure.Cosmos.Samples/Usage/Queries).

Comments and suggestions welcomed!