namespace Microsoft.Learn.NoSQLValidation.UnitTests.Fixtures;

public sealed class CosmosDbFixture : IDisposable
{
    private readonly CosmosClientOptions clientOptions = new()
    {
        HttpClientFactory = () => new HttpClient(
            new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            }
        ),
        ConnectionMode = ConnectionMode.Gateway
    };

    private CosmosClient Client { get; set; }

    public Container Container { get; private set; }

    public CosmosDbFixture()
    {
        string connectionString = Environment.GetEnvironmentVariable("COSMOSDB_CONNECTIONSTRING")
            ?? throw new InvalidOperationException("Missing connection string");

        Client = new CosmosClient(connectionString, clientOptions);

        var databaseTask = Client.CreateDatabaseIfNotExistsAsync($"validation-automated", 400);
        Database database = databaseTask.Result;

        var containerTask = database.CreateContainerIfNotExistsAsync($"data-automated", "/pk");
        Container = containerTask.Result;
    }

    public void Dispose()
    {
        Client?.Dispose();
    }
}