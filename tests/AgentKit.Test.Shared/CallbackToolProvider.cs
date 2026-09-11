// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Exposes typed source-metadata, discovery, and ownership probes for registration and capture tests.</summary>
public sealed class CallbackToolProvider: IToolProvider, IAsyncDisposable
{
    private int _identityReads;
    private int _discoveries;
    private int _disposals;

    /// <summary>Captures an explicit source ID, including standard keyed-DI constructor injection.</summary>
    /// <param name="sourceId">The nondefault exact source registration key.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceId"/> is default.</exception>
    public CallbackToolProvider([ServiceKey] ToolSourceId sourceId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sourceId, default);
        SourceId = sourceId;
    }

    /// <summary>Gets or sets an optional metadata callback for deliberate provider-contract failures.</summary>
    /// <value>Null returns the constructor identity; a callback may throw or substitute identity in adversarial tests.</value>
    public Func<ToolSourceId>? ReadSourceId { get; set; }

    /// <summary>Gets or sets the optional discovery callback.</summary>
    /// <value>Null fails if discovery is attempted; selection tests leave this unset to prove no discovery occurs.</value>
    public Func<ToolDiscoveryRequest, CancellationToken, ValueTask<IToolProviderCapture>>? Discover { get; set; }

    /// <summary>Gets the number of metadata reads observed so far.</summary>
    /// <value>A thread-safe point-in-time count.</value>
    public int IdentityReads => Volatile.Read(ref _identityReads);

    /// <summary>Gets the number of discovery calls observed so far.</summary>
    /// <value>A thread-safe point-in-time count.</value>
    public int Discoveries => Volatile.Read(ref _discoveries);

    /// <summary>Gets the number of disposal calls observed so far.</summary>
    /// <value>A thread-safe point-in-time count for ownership assertions.</value>
    public int Disposals => Volatile.Read(ref _disposals);

    /// <inheritdoc/>
    public ToolSourceId SourceId
    {
        get
        {
            _ = Interlocked.Increment(ref _identityReads);
            return ReadSourceId?.Invoke() ?? field;
        }
    }

    /// <inheritdoc/>
    public ValueTask<IToolProviderCapture> DiscoverAsync(ToolDiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        _ = Interlocked.Increment(ref _discoveries);
        return Discover?.Invoke(request, cancellationToken) ?? throw new InvalidOperationException("Discovery was not configured for this test.");
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _ = Interlocked.Increment(ref _disposals);
        return ValueTask.CompletedTask;
    }
}
