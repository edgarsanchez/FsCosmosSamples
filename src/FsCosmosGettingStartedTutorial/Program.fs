open System
open System.Net
open Microsoft.Azure.Cosmos
open Model

/// The Azure Cosmos DB endpoint for running this sample.
let endpointUri = "https://PUT-YOUR-COSMOS-DB-ACCOUNT-HERE/"
/// The primary key for the Azure Cosmos account.
let primaryKey = "PUT-YOUR-AZURE-COSMOS-DB-ACCOUNT-PRIMARY-KEY-HERE"

// The name of the database and container we will create.
let databaseId = "FamilyDatabase"
let containerId = "FamilyContainer"

/// Create the database if it does not exist
let createDatabaseAsync (cosmosClient: CosmosClient) =
    async {
        // Create a new database
        let! dbResponse = cosmosClient.CreateDatabaseIfNotExistsAsync databaseId |> Async.AwaitTask
        printfn "Database created"
        return dbResponse.Database
    }

/// Create the container if it does not exist. 
/// Specifiy "/LastName" as the partition key since we're storing family information, to ensure good distribution of requests and storage.
let createContainerAsync (database: Database) =
    async {
        // Create a new container
        let! cntrResponse = database.CreateContainerIfNotExistsAsync (containerId, "/LastName") |> Async.AwaitTask
        printfn "Created Container: %s\n" cntrResponse.Container.Id
        return cntrResponse.Container
    }

/// Add Family items to the container
let addItemsToContainerAsync (container: Container) =
    async {
        // Create a family object for the Andersen family
        let andersenFamily = 
            { Id = "Andersen.1"
              LastName = "Andersen"
              Parents = [|
                { FirstName = "Thomas"; FamilyName = null }
                { FirstName = "Mary Kay"; FamilyName = null } |]
              Children = [|
                {  FamilyName = null
                   FirstName = "Henriette Thaulow"
                   Gender = "female"
                   Grade = 5
                   Pets = [| { GivenName = "Fluffy" } |] } |]
              Address = { State = "WA"; County = "King"; City = "Seattle" }
              IsRegistered = false }

        try
            // Read the item to see if it exists.  
            let! andersenFamilyResponse = 
                container.ReadItemAsync (andersenFamily.Id, PartitionKey (andersenFamily.LastName)) |> Async.AwaitTask
            printfn "Item in database with id: %s already exists\n" andersenFamilyResponse.Resource.Id
        with
        | :? AggregateException as ex ->
            match Seq.head ex.InnerExceptions with
            | :? CosmosException as exInner when exInner.StatusCode = HttpStatusCode.NotFound ->
                // Create an item in the container representing the Andersen family. Note we provide the value of the partition key for this item, which is "Andersen"
                let! andersenFamilyResponse = 
                    container.CreateItemAsync (andersenFamily, Nullable (PartitionKey (andersenFamily.LastName))) |> Async.AwaitTask
                
                // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                printfn "Created item in database with id: %s Operation consumed %f RUs.\n" andersenFamilyResponse.Resource.Id andersenFamilyResponse.RequestCharge
            | otherEx ->
                raise otherEx

        // Create a family object for the Wakefield family
        let wakefieldFamily = 
            { Id = "Wakefield.7"
              LastName = "Wakefield"
              Parents = [|
                { FamilyName = "Wakefield"; FirstName = "Robin" }
                { FamilyName = "Miller"; FirstName = "Ben" } |]
              Children = [|
                {  FamilyName = "Merriam"
                   FirstName = "Jesse"
                   Gender = "female"
                   Grade = 8
                   Pets = [| { GivenName = "Goofy" }; { GivenName = "Shadow" } |] }
                { FamilyName = "Miller"
                  FirstName = "Lisa"
                  Gender = "female"
                  Grade = 1 
                  Pets = null } |]
              Address = { State = "NY"; County = "Manhattan"; City = "NY" }
              IsRegistered = false }

        try
            // Read the item to see if it exists
            let! wakefieldFamilyResponse = 
                container.ReadItemAsync (wakefieldFamily.Id, PartitionKey (wakefieldFamily.LastName)) |> Async.AwaitTask
            printfn "Item in database with id: %s already exists\n" wakefieldFamilyResponse.Resource.Id
        with
        | :? AggregateException as ex ->
            match Seq.head ex.InnerExceptions with
            | :? CosmosException as exInner when exInner.StatusCode = HttpStatusCode.NotFound ->
                // Create an item in the container representing the Wakefield family. Note we provide the value of the partition key for this item, which is "Wakefield"
                let! wakefieldFamilyResponse = 
                    container.CreateItemAsync (wakefieldFamily, Nullable( PartitionKey (wakefieldFamily.LastName))) |> Async.AwaitTask

                // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                printfn "Created item in database with id: %s Operation consumed %f RUs.\n" wakefieldFamilyResponse.Resource.Id wakefieldFamilyResponse.RequestCharge
            | otherEx ->
                raise otherEx
    }

/// Run a query (using Azure Cosmos DB SQL syntax) against the container.
let queryItemsAsync (container: Container) =
    async {
        let sqlQueryText = "SELECT * FROM c WHERE c.LastName = 'Andersen'"

        printfn "Running query: %s\n" sqlQueryText

        let queryDefinition = QueryDefinition (sqlQueryText)
        use queryResultSetIterator = container.GetItemQueryIterator queryDefinition

        let families = ResizeArray<Family> ()

        while queryResultSetIterator.HasMoreResults do
            let! currentResultSet = queryResultSetIterator.ReadNextAsync () |> Async.AwaitTask

            for family in currentResultSet do
                families.Add family
                printfn "\tRead %O\n" family
    }

/// Replace an item in the container.
let replaceFamilyItemAsync (container: Container) =
    async {
        let! wakefieldFamilyResponse = container.ReadItemAsync ("Wakefield.7", PartitionKey ("Wakefield")) |> Async.AwaitTask
        let itemBody = wakefieldFamilyResponse.Resource

        // update registration status from false to true
        let newItemBody = { itemBody with IsRegistered = true }
        // update grade of child
        newItemBody.Children.[0] <- { newItemBody.Children.[0] with Grade = 6 }

        // replace the item with the updated content
        let! wakefieldFamilyResponse = 
            container.ReplaceItemAsync (newItemBody, itemBody.Id, Nullable (PartitionKey (itemBody.LastName))) |> Async.AwaitTask
        printfn "Updated Family [%s,%s].\n \tBody is now: %O\n" newItemBody.LastName newItemBody.Id wakefieldFamilyResponse.Resource
    }

/// Delete an item in the container.
let deleteFamilyItemAsync (container: Container) =
    async {
        let partitionKeyValue = "Wakefield"
        let familyId = "Wakefield.7"

        // Delete an item. Note we must provide the partition key value and id of the item to delete
        let! _ = container.DeleteItemAsync (familyId, PartitionKey (partitionKeyValue)) |> Async.AwaitTask
        printfn "Deleted Family [%s,%s]\n" partitionKeyValue familyId
    }

/// Delete the database and dispose of the Cosmos Client instance.
let deleteDatabaseAndCleanupAsync (cosmosClient: CosmosClient) (database: Database) =
    async {
        let! _ = database.DeleteAsync () |> Async.AwaitTask
        // Also valid: cosmosClient.GetDatabase("FamilyDatabase").DeleteAsync ()

        printfn "Deleted Database: %s\n" databaseId

        //Dispose of CosmosClient
        cosmosClient.Dispose ()
    }

///Entry point to call methods that operate on Azure Cosmos DB resources in this sample
let getStartedDemoAsync () =
    async {
        // Create a new instance of the Cosmos Client
        use cosmosClient = new CosmosClient (endpointUri, primaryKey)

        let! database = createDatabaseAsync cosmosClient
        let! container = createContainerAsync database
        do! addItemsToContainerAsync container
        do! queryItemsAsync container
        do! replaceFamilyItemAsync container
        do! deleteFamilyItemAsync container
        do! deleteDatabaseAndCleanupAsync cosmosClient database
    }

[< EntryPoint >]
let main _ =
    try
        try
            printfn "Beginning operations...\n"
            getStartedDemoAsync () |> Async.RunSynchronously
        with
        | :? CosmosException as de ->
            // let baseException = de.GetBaseException ()
            printfn "%O error ocurred: %O" de.StatusCode de
        | e ->
            printfn "Error: %O" e
    finally
        printfn "End of demo, press any key to exit."
        Console.ReadKey () |> ignore
    
    0
