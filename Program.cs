using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Spectre.Console;

public class Program
{
    const string databaseName = "RamTest";
    const string containerName = "test";

    public static async Task Main()
    {
        var client = GetClient();
        Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName);
        Container container = await database.CreateContainerIfNotExistsAsync(containerName, "/PartitionKey");

        AnsiConsole.Write(new FigletText("Sample")
            .LeftAligned()
            );

        using var cancel = new CancellationTokenSource();
        var exited = false;
        var createItemsCount = 10000;
        var readItemsLoops = 100;
        while (!exited)
        {
            var command = AnsiConsole.Prompt<CommandType>(new SelectionPrompt<CommandType>()
                .AddChoices(Enum.GetValues<CommandType>()));
            switch (command)
            {
                case CommandType.CreateEntries:
                    await CreateEntries(container, createItemsCount, cancel.Token);
                    break;
                case CommandType.SelectEntries:
                    await SelectEntries(container, readItemsLoops, cancel.Token);
                    break;
                case CommandType.Configuration:
                    createItemsCount = AnsiConsole.Ask<int>("Items to create:", createItemsCount);
                    readItemsLoops = AnsiConsole.Ask<int>("Read count:", readItemsLoops);
                    break;
                case CommandType.DeleteDatabaseAndExit:
                    await database.DeleteAsync();
                    exited = true;
                    break;
                case CommandType.Exit:
                    exited = true;
                    break;
            }
        }
    }

    public static async Task CreateEntries(Container container, int count, CancellationToken cancel)
    {
        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),    // Task description
                new ProgressBarColumn(),        // Progress bar
                new PercentageColumn(),         // Percentage
                new SpinnerColumn(),            // Spinner
            })
            .StartAsync(async ctx =>
            {
                var progress = ctx.AddTask("Schreiben", maxValue: count);
                progress.StartTask();
                await Parallel.ForEachAsync(Enumerable.Range(0, count), async (i, c) =>
                {
                    var document = new MyDocument()
                    {
                        id = Guid.NewGuid(),
                        Value = i,
                    };
                    await container.CreateItemAsync(document, cancellationToken: cancel);
                    progress.Increment(1);
                });
                progress.StopTask();
            });
    }

    public static async Task SelectEntries(Container container, int count, CancellationToken cancel)
    {
        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),    // Task description
                new ProgressBarColumn(),        // Progress bar
                new PercentageColumn(),         // Percentage
                new SpinnerColumn(),            // Spinner
            })
            .StartAsync(async ctx =>
            {
                var progress = ctx.AddTask("Lesen", maxValue: count);
                progress.StartTask();
                var options = new ParallelOptions()
                {
                    MaxDegreeOfParallelism = count,
                    CancellationToken = cancel,
                };
                await Parallel.ForEachAsync(Enumerable.Range(0, count), options, async (i, c) =>
                {
                    var count = 0;
                    await foreach (var item in GetAllItems<object>(container, cancel))
                    {
                        count++;
                    }
                    progress.Increment(1);
                });
                progress.StopTask();
            });
    }

    public static async IAsyncEnumerable<T> GetAllItems<T>(Container container, [EnumeratorCancellation] CancellationToken cancel)
    {
        var feed = container.GetItemLinqQueryable<T>(requestOptions: new QueryRequestOptions());
        using (var iterator = feed.ToFeedIterator())
        {
            while (iterator.HasMoreResults)
            {
                FeedResponse<T>? response;
                try
                {
                    response = await iterator.ReadNextAsync(cancel);
                    HandleCosmosDbResponse(response);
                }
                catch (Exception ex)
                {
                    HandleCosmosDbException(ex);
                    throw;
                }
                if (response is not null)
                {
                    foreach (var item in response.Resource)
                        yield return item;
                }
            }
        }
    }

    public static async ValueTask<ImmutableArray<T>> ToImmutableArrayAsync<T>(IAsyncEnumerable<T> enumerable, CancellationToken cancel)
    {
        var builder = ImmutableArray.CreateBuilder<T>();
        await foreach (var value in enumerable.WithCancellation(cancel))
            builder.Add(value);
        return builder.ToImmutable();
    }

    private static void HandleCosmosDbException(Exception ex) { }
    private static void HandleCosmosDbResponse<T>(FeedResponse<T> response) { }

    public static CosmosClient GetClient()
    {
        var primaryKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        var httpsUri = "https://localhost:8081";
        var wsUri = "ws://localhost:8901";

        return new CosmosClient(httpsUri, primaryKey, new CosmosClientOptions() { ApplicationName = "Tests" });
    }
}

public class MyDocument
{
    public Guid id { get; set; }
    public int Value { get; set; }
}

public enum CommandType
{
    CreateEntries,
    SelectEntries,
    Configuration,
    DeleteDatabaseAndExit,
    Exit,
}