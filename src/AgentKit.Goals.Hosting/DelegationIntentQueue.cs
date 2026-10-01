// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting;

/// <summary>Is the in-process wake-up path from the local dispatcher to the worker.</summary>
/// <remarks>
/// The queue is bounded and lossy by design: a full queue drops the hint because the intent is already durable and the worker's periodic scan rediscovers it. It never blocks the dispatcher, and it is thread-safe for any number of writers and the worker's single reader.
/// A signal also activates the worker when nothing has started it: a host-managed composition starts the hosted worker at startup, but a standalone engine has no host, so the first committed intent starts it. Activation is idempotent and is handed in as a callback so the queue never constructor-depends on the worker that reads it.
/// </remarks>
internal sealed class DelegationIntentQueue: IDelegationIntentSignal
{
    private readonly Channel<DelegationIntent> _channel;
    private readonly Func<Task>? _activate;

    /// <summary>Initializes the queue.</summary>
    /// <param name="options">The worker options carrying the queue capacity.</param>
    /// <param name="activate">Starts the worker if it is not running, or <see langword="null"/> when a host always starts it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public DelegationIntentQueue(IOptions<GoalWorkerOptions> options, Func<Task>? activate = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _activate = activate;
        _channel = Channel.CreateBounded<DelegationIntent>(new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>Gets the reader the worker drains.</summary>
    internal ChannelReader<DelegationIntent> Reader => _channel.Reader;

    /// <inheritdoc/>
    public async ValueTask SignalAsync(DelegationIntent intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        _ = _channel.Writer.TryWrite(intent);
        if (_activate is not null)
        {
            await _activate().ConfigureAwait(false);
        }
    }
}
