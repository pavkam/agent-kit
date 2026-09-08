// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Publishes one diagnostics instrument without blocking or reentering its creation callback.</summary>
/// <typeparam name="TInstrument">The non-null reference instrument type.</typeparam>
/// <remarks>
/// Instrument publication synchronously calls arbitrary meter listeners. A
/// reentrant caller on the publishing thread therefore observes an unavailable
/// instrument instead of recursively publishing. Concurrent callers may create
/// candidates without waiting and use the candidate whose listeners they
/// synchronously notified; atomic publication selects one value for later
/// callers to reuse. Creation failure is observational and permits a later retry.
/// </remarks>
internal sealed class NonBlockingInstrument<TInstrument>
    where TInstrument : class
{
    [ThreadStatic]
    private static HashSet<NonBlockingInstrument<TInstrument>>? _publishingOnThread;

    private TInstrument? _instrument;

    /// <summary>Gets the published instrument or attempts one nonblocking publication.</summary>
    /// <param name="create">The non-null instrument factory, which may synchronously invoke arbitrary listeners.</param>
    /// <returns>
    /// The previously published instrument or this caller's successfully
    /// created candidate. Returns <see langword="null"/> for same-thread
    /// reentry, factory failure, or a factory that produces null.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="create"/> is <see langword="null"/>.</exception>
    internal TInstrument? GetOrCreate(Func<TInstrument> create)
    {
        ArgumentNullException.ThrowIfNull(create);

        var instrument = Volatile.Read(ref _instrument);
        if (instrument is not null)
        {
            return instrument;
        }

        var publishing = _publishingOnThread ??= new(ReferenceEqualityComparer.Instance);
        if (!publishing.Add(this))
        {
            return null;
        }

        try
        {
            instrument = create();
            if (instrument is null)
            {
                return null;
            }

            _ = Interlocked.CompareExchange(ref _instrument, instrument, comparand: null);
            return instrument;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            _ = publishing.Remove(this);
        }
    }
}
