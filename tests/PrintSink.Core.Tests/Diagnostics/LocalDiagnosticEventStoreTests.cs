using PrintSink.Core.Diagnostics;

namespace PrintSink.Core.Tests.Diagnostics;

/// <summary>
/// Tests local diagnostic event persistence.
/// </summary>
[TestClass]
internal sealed class LocalDiagnosticEventStoreTests
{
    /// <summary>
    /// Gets or sets the MSTest context for cancellation-aware async work.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies missing diagnostic storage returns no events.
    /// </summary>
    [TestMethod]
    public async Task ReadRecentAsyncReturnsEmptyWhenMissing()
    {
        string directory = CreateTestDirectory();
        using LocalDiagnosticEventStore store = new(directory);

        try
        {
            IReadOnlyList<DiagnosticEventRecord> records = await store
                .ReadRecentAsync(4, TestContext.CancellationToken)
                .ConfigureAwait(false);

            Assert.IsEmpty(records);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies recent diagnostics are returned newest first.
    /// </summary>
    [TestMethod]
    public async Task ReadRecentAsyncReturnsNewestEventsFirst()
    {
        string directory = CreateTestDirectory();
        using LocalDiagnosticEventStore store = new(directory);

        try
        {
            await store
                .AppendAsync(CreateRecord("First", DateTimeOffset.UtcNow.AddMinutes(-1)), TestContext.CancellationToken)
                .ConfigureAwait(false);
            await store
                .AppendAsync(CreateRecord("Second", DateTimeOffset.UtcNow), TestContext.CancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<DiagnosticEventRecord> records = await store
                .ReadRecentAsync(2, TestContext.CancellationToken)
                .ConfigureAwait(false);

            Assert.HasCount(2, records);
            Assert.AreEqual("Second", records[0].Message);
            Assert.AreEqual("First", records[1].Message);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies the local store trims older diagnostics.
    /// </summary>
    [TestMethod]
    public async Task AppendAsyncTrimsOldEvents()
    {
        string directory = CreateTestDirectory();
        using LocalDiagnosticEventStore store = new(directory, 2);

        try
        {
            await store
                .AppendAsync(CreateRecord("One", DateTimeOffset.UtcNow.AddMinutes(-2)), TestContext.CancellationToken)
                .ConfigureAwait(false);
            await store
                .AppendAsync(CreateRecord("Two", DateTimeOffset.UtcNow.AddMinutes(-1)), TestContext.CancellationToken)
                .ConfigureAwait(false);
            await store
                .AppendAsync(CreateRecord("Three", DateTimeOffset.UtcNow), TestContext.CancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<DiagnosticEventRecord> records = await store
                .ReadRecentAsync(8, TestContext.CancellationToken)
                .ConfigureAwait(false);

            Assert.HasCount(2, records);
            Assert.AreEqual("Three", records[0].Message);
            Assert.AreEqual("Two", records[1].Message);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies concurrent store instances preserve all appended diagnostics.
    /// </summary>
    [TestMethod]
    public async Task AppendAsyncPreservesEventsFromConcurrentStoreInstances()
    {
        string directory = CreateTestDirectory();
        const int EventCount = 32;

        try
        {
            Task[] appendTasks = [.. Enumerable.Range(0, EventCount).Select(async index =>
            {
                using LocalDiagnosticEventStore store = new(directory, EventCount);
                await store
                    .AppendAsync(
                        CreateRecord($"Event {index}", DateTimeOffset.UtcNow.AddSeconds(index)),
                        TestContext.CancellationToken)
                    .ConfigureAwait(false);
            })];
            await Task.WhenAll(appendTasks).ConfigureAwait(false);

            using LocalDiagnosticEventStore reader = new(directory, EventCount);
            IReadOnlyList<DiagnosticEventRecord> records = await reader
                .ReadRecentAsync(EventCount, TestContext.CancellationToken)
                .ConfigureAwait(false);

            HashSet<string> messages = [.. records.Select(record => record.Message)];
            Assert.HasCount(EventCount, records);
            for (int index = 0; index < EventCount; index++)
            {
                Assert.Contains($"Event {index}", messages);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static DiagnosticEventRecord CreateRecord(string message, DateTimeOffset timestamp)
    {
        return new DiagnosticEventRecord(
            timestamp,
            DiagnosticEventSeverity.Information,
            "Test",
            message,
            "PrintSink - PDF",
            null);
    }

    private static string CreateTestDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PrintSink.Tests", Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        return directory;
    }
}
