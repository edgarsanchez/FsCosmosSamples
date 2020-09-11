open System
open System.IO
open Microsoft.Extensions.Configuration
open Microsoft.Azure.Cosmos
open Newtonsoft.Json
open Newtonsoft.Json.Linq

/// These functions show the different ways to execute item feed and queries.
/// For help with SQL query syntax see: 
/// https://docs.microsoft.com/en-us/azure/cosmos-db/query-cheat-sheet
/// https://docs.microsoft.com/en-us/azure/cosmos-db/how-to-sql-query

type Parent = 
    { FamilyName: string
      FirstName: string }

type Pet = 
    { GivenName: string }

type Child = 
    { FamilyName: string
      FirstName: string
      Gender: string
      Grade: int
      Pets: Pet[] }

type Address = 
    { State: string
      County: string
      City: string }

type Family = 
    { [< JsonProperty (PropertyName = "id") >]
      Id: string
      LastName: string
      Parents: Parent[]
      Children: Child[]
      Address: Address
      IsRegistered: bool 
      RegistrationDate: DateTime }

    member __.PartitionKey = __.LastName
    static member PartitionKeyPath = "/LastName"

let inline nullPKey (value: string) = Nullable (PartitionKey (value))

let ``assert`` message condition =
    if not condition then
        raise (ApplicationException (message))

let assertSequenceEqual message (list1: ResizeArray<Family>) list2 =
    let l1 = list1 |> Seq.map (fun x -> x.Id) |> Seq.toList
    let l2 = list2 |> Seq.map (fun x -> x.Id) |> Seq.toList
    if l1 <> l2 then
        raise (ApplicationException (message))

let itemFeed (container: Container) =
    async {
        let families = ResizeArray<Family> []

        //SQL
        use setIterator = container.GetItemQueryIterator (requestOptions = QueryRequestOptions (MaxItemCount = Nullable 1))
        while setIterator.HasMoreResults do
            let count = 0
            let! feedResponse = setIterator.ReadNextAsync () |> Async.AwaitTask
            for item in feedResponse do
                ``assert`` "Should only return 1 result at a time." (count <= 1)
                families.Add item

        ``assert`` "Expected two families" (families.Count = 2)
    }

let itemStreamFeed (container: Container) =
    async {
        let mutable totalCount = 0

        //SQL
        use setIterator = container.GetItemQueryStreamIterator ()
        while setIterator.HasMoreResults do
            let mutable count = 0
            use! response = setIterator.ReadNextAsync () |> Async.AwaitTask
            if not (isNull response.Diagnostics) then
                printfn "ItemStreamFeed Diagnostics: %O" response.Diagnostics

            response.EnsureSuccessStatusCode () |> ignore
            count <- count + 1
            use sr = new StreamReader (response.Content)
            use jtr = new JsonTextReader (sr)
            let jsonSerializer = JsonSerializer ()
            let jobj = jsonSerializer.Deserialize jtr :?> JObject
            totalCount <- totalCount + (jobj.["Documents"] :?> JArray).Count

        ``assert`` "Expected two families" (totalCount = 2)
    }

let queryItemsInPartitionAsStreams (container: Container) =
    // SQL
    use setIterator = container.GetItemQueryStreamIterator (
                        "SELECT F.id, F.LastName, F.IsRegistered FROM Families F",
                        requestOptions = QueryRequestOptions (
                            PartitionKey = nullPKey "Anderson",
                            MaxConcurrency = Nullable 1,
                            MaxItemCount = Nullable 1 ) )

    let mutable count = 0
    async {
        while setIterator.HasMoreResults do
            use! response = setIterator.ReadNextAsync () |> Async.AwaitTask
            ``assert`` "Response failed" response.IsSuccessStatusCode
            count <- count + 1
            use sr = new StreamReader (response.Content)
            use jtr = new JsonTextReader (sr)
            let jsonSerializer = JsonSerializer ()
            let jobj = jsonSerializer.Deserialize jtr :?> JObject
            let items = jobj.["Documents"] :?> JArray
            ``assert`` "Expected one family" (items.Count = 1)
            let item = items.[0]
            ``assert`` (sprintf "Expected LastName: Anderson Actual: %O" item.["LastName"])
                       (String.Equals ("Anderson", string item.["LastName"], StringComparison.InvariantCulture))

        ``assert`` "Expected 1 family" (count = 1)
    }

let queryWithSqlParameters (container: Container) =
    // Query using two properties within each item. WHERE Id == "" AND Address.City == ""
    // notice here how we are doing an equality comparison on the string value of City

    let query = QueryDefinition("SELECT * FROM Families f WHERE f.id = @id AND f.Address.City = @city")
                    .WithParameter("@id", "AndersonFamily")
                    .WithParameter ("@city", "Seattle")

    let results = ResizeArray<Family> ()
    use resultSetIterator = container.GetItemQueryIterator (
                                query,
                                requestOptions = QueryRequestOptions (PartitionKey = nullPKey "Anderson") )
    
    async {
        while resultSetIterator.HasMoreResults do
            let! response = resultSetIterator.ReadNextAsync () |> Async.AwaitTask
            results.AddRange response
            if not (isNull response.Diagnostics) then
                printfn "\nQueryWithSqlParameters Diagnostics: %O" response.Diagnostics

        ``assert`` "Expected 1 family" (results.Count = 1)
    }

let queryPartitionedContainerInParallelAsync (container: Container) =
    let familiesSerial = ResizeArray<Family> ()
    let queryText = "SELECT * FROM Families"

    // 0 maximum parallel tasks, effectively serial execution
    let options = QueryRequestOptions (MaxBufferedItemCount = Nullable 100, MaxConcurrency = Nullable 0)
    use query = container.GetItemQueryIterator (queryText, requestOptions = options)
    async {
        while query.HasMoreResults do
            let! response = query.ReadNextAsync() |> Async.AwaitTask
            response |> Seq.iter familiesSerial.Add

        ``assert`` "Serial Query expected two families" (familiesSerial.Count = 2)

        // 1 maximum parallel tasks, 1 dedicated asynchronous task to continuously make REST calls
        let familiesParallel1 = ResizeArray<Family> ()

        options.MaxConcurrency <- Nullable 1
        use query' = container.GetItemQueryIterator (queryText, requestOptions = options)
        while query'.HasMoreResults do
            let! response' = query'.ReadNextAsync () |> Async.AwaitTask
            response' |> Seq.iter familiesParallel1.Add

        ``assert`` "Parallel Query expected two families" (familiesParallel1.Count = 2)
        assertSequenceEqual "Parallel query returns result out of order compared to serial execution" familiesSerial familiesParallel1


        // 10 maximum parallel tasks, a maximum of 10 dedicated asynchronous tasks to continuously make REST calls
        let familiesParallel10 = ResizeArray<Family> ()

        options.MaxConcurrency <- Nullable 10
        use query'' = container.GetItemQueryIterator (queryText, requestOptions = options)
        while query''.HasMoreResults do
            let! response'' = query''.ReadNextAsync () |> Async.AwaitTask
            response'' |> Seq.iter familiesParallel10.Add

        ``assert`` "Parallel Query expected two families" (familiesParallel10.Count = 2)
        assertSequenceEqual "Parallel query returns result out of order compared to serial execution" familiesSerial familiesParallel10
    }

/// Get a DocuemntContainer by id, or create a new one if one with the id provided doesn't exist.
/// Receive the id of the CosmosContainer to search for, or create.
/// Returns the matched, or created, CosmosContainer object.
let getOrCreateContainerAsync (database: Database) containerId =
    async {
        let containerProperties = ContainerProperties (containerId, Family.PartitionKeyPath)

        let! response = database.CreateContainerIfNotExistsAsync (containerProperties = containerProperties, throughput = Nullable 400) |> Async.AwaitTask
        return response.Container
    }

/// Creates the items used in this Sample
/// Receives as parameter the Cosmos DB container where the items will be created
let createItems (container: Container) =
    async {
        let andersonFamily = 
            { Id = "AndersonFamily"
              LastName = "Anderson"
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
              IsRegistered = false
              RegistrationDate = DateTime.UtcNow.AddDays -1. }

        let! _ = container.UpsertItemAsync (andersonFamily, nullPKey andersonFamily.PartitionKey) |> Async.AwaitTask

        let wakefieldFamily = 
            { Id = "WakefieldFamily"
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
                { FamilyName = null
                  FirstName = "Lisa"
                  Gender = "female"
                  Grade = 1 
                  Pets = null } |]
              Address = { State = "NY"; County = "Manhattan"; City = "NY" }
              IsRegistered = false
              RegistrationDate = DateTime.UtcNow.AddDays -30. }

        let! _ = container.UpsertItemAsync (wakefieldFamily, nullPKey wakefieldFamily.PartitionKey) |> Async.AwaitTask
        ()
    }

//Read configuration
let cosmosDatabaseId = "samples"
let containerId = "query-samples"

let runDemoAsync (client: CosmosClient) =
    async {
        let! dbResponse = client.CreateDatabaseIfNotExistsAsync cosmosDatabaseId |> Async.AwaitTask
        let! container = getOrCreateContainerAsync dbResponse.Database containerId

        do! createItems container
        do! itemFeed container
        do! itemStreamFeed container
        do! queryItemsInPartitionAsStreams container
        do! queryWithSqlParameters container
        do! queryPartitionedContainerInParallelAsync container

        // Uncomment to Cleanup
        // let! _ = dbResponse.Database.DeleteAsync () |> Async.AwaitTask
        // ()
    }

[< EntryPoint >]
let main _ =
    try
        try
            let configuration = ConfigurationBuilder().AddJsonFile("appSettings.json").Build ()

            let endpoint = configuration.["EndPointUrl"]
            if String.IsNullOrEmpty endpoint then
                raise (ArgumentNullException ("Please specify a valid endpoint in the appSettings.json"))

            let authKey = configuration.["AuthorizationKey"]
            if String.IsNullOrEmpty endpoint || authKey = "Super secret key" then
                raise (ArgumentNullException ("Please specify a valid AuthorizationKey in the appSettings.json"))

            //Read the Cosmos endpointUrl and authorisationKeys from configuration
            //These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys"
            //NB > Keep these values in a safe & secure location. Together they provide Administrative access to your Cosmos account
            use client = new CosmosClient (endpoint, authKey)
            runDemoAsync client |> Async.RunSynchronously
        with
        | :? CosmosException as cre ->
            printfn "%O" cre
        | e ->
            let baseException = e.GetBaseException ()
            printfn "Error: %s, Message: %s" e.Message baseException.Message
    finally
        printfn "End of demo, press any key to exit."
        Console.ReadKey () |> ignore

    0 
