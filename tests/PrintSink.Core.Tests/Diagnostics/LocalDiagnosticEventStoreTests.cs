using PrintSink.Core.Diagnostics;

namespace PrintSink.Core.Tests.Diagnostics;

/// <summary>
/// Tests local diagnostic event persistence.
/// </summary>
[TestClass]
public sealed class LocalDiagnosticEventStoreTests
{
    /// <summary>
    /// Gets or sets the MSTest context for cancellation-aware async work.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies missing diagnostic storage returns no events.
    /// </summary>
    [TestMethod]
    public async Task ReadRecentAsync_returns_empty_when_missing()
    {
        string directory = CreateTestDirectory();
        LocalDiagnosticEventStore store = new(directory);

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
    public async Task ReadRecentAsync_returns_newest_events_first()
    {
        string directory = CreateTestDirectory();
        LocalDiagnosticEventStore store = new(directory);

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
    public async Task AppendAsync_trims_old_events()
    {
        string directory = CreateTestDirectory();
        LocalDiagnosticEventStore store = new(directory, 2);

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
