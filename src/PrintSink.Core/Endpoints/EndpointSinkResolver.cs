namespace PrintSink.Core.Endpoints;

/// <summary>
/// Resolves endpoint sinks from an endpoint-kind map.
/// </summary>
public sealed class EndpointSinkResolver : IEndpointSinkResolver
{
    private readonly IReadOnlyDictionary<EndpointKind, ISink> sinks;

    /// <summary>
    /// Initializes a new instance of the <see cref="EndpointSinkResolver"/> class.
    /// </summary>
    /// <param name="sinks">The sinks keyed by endpoint kind.</param>
    public EndpointSinkResolver(IReadOnlyDictionary<EndpointKind, ISink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);

        this.sinks = sinks;
    }

    /// <inheritdoc />
    public ISink Resolve(VirtualEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return sinks.TryGetValue(endpoint.Kind, out ISink? sink)
            ? sink
            : throw new InvalidOperationException($"No sink is registered for endpoint '{endpoint.QueueName}'.");
    }
}
