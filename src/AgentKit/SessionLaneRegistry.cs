// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The engine's process-local guarantee that one session drives at most one run at a time.
/// </summary>
/// <remarks>
/// <para>
/// Lanes are keyed by agent and session. Entering a lane either acquires it immediately, waits for the active run
/// to release it (<see cref="SessionBusyBehavior.Wait"/>), or fails with <see cref="AgentSessionBusyException"/>
/// (<see cref="SessionBusyBehavior.Reject"/>). A lane is forgotten as soon as nobody holds or waits on it, so the
/// registry stays bounded by the number of concurrently active sessions.
/// </para>
/// <para>
/// This is a synchronization gate, not persistence: it protects one engine instance. Cross-process exclusion is
/// the session run coordinator's durable lease, which profiles requiring distributed fencing must select.
/// </para>
/// </remarks>
internal sealed class SessionLaneRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(AgentId AgentId, SessionId SessionId), Lane> _lanes = [];

    /// <summary>Enters the lane for one session, applying the profile's busy behavior.</summary>
    /// <param name="agentId">The agent the session belongs to.</param>
    /// <param name="sessionId">The session to drive.</param>
    /// <param name="runId">The run that will hold the lane, reported to rejected contenders.</param>
    /// <param name="busyBehavior">Whether a contender waits for or is rejected by an active run.</param>
    /// <param name="cancellationToken">Cancels a wait; a cancelled wait holds nothing.</param>
    /// <returns>A lease that releases the lane when disposed.</returns>
    /// <exception cref="AgentSessionBusyException">The lane is held and <paramref name="busyBehavior"/> is <see cref="SessionBusyBehavior.Reject"/>.</exception>
    /// <exception cref="OperationCanceledException">The wait was cancelled.</exception>
    public async ValueTask<Lease> EnterAsync(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        SessionBusyBehavior busyBehavior,
        CancellationToken cancellationToken)
    {
        Debug.Assert(agentId != default && sessionId != default && runId != default, "Callers pass allocated identities.");
        var key = (agentId, sessionId);
        Lane lane;
        lock (_gate)
        {
            if (!_lanes.TryGetValue(key, out lane!))
            {
                lane = new Lane();
                _lanes[key] = lane;
            }

            lane.Interested++;
        }

        try
        {
            if (busyBehavior == SessionBusyBehavior.Reject)
            {
                if (!lane.Gate.Wait(0, CancellationToken.None))
                {
                    throw new AgentSessionBusyException(agentId, sessionId, lane.ActiveRunId);
                }
            }
            else
            {
                await lane.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            Release(key, lane, held: false);
            throw;
        }

        lane.ActiveRunId = runId;
        return new Lease(this, key, lane);
    }

    private void Release((AgentId, SessionId) key, Lane lane, bool held)
    {
        lock (_gate)
        {
            if (held)
            {
                lane.ActiveRunId = default;
                _ = lane.Gate.Release();
            }

            lane.Interested--;
            if (lane.Interested == 0 && _lanes.TryGetValue(key, out var current) && ReferenceEquals(current, lane))
            {
                _ = _lanes.Remove(key);
                lane.Gate.Dispose();
            }
        }
    }

    /// <summary>One session's gate and the run currently holding it.</summary>
    internal sealed class Lane
    {
        /// <summary>Gets the single-occupancy gate.</summary>
        public SemaphoreSlim Gate { get; } = new(1, 1);

        /// <summary>Gets or sets the run that holds the gate, or default when free.</summary>
        public RunId ActiveRunId { get; set; }

        /// <summary>Gets or sets how many callers hold or wait on the lane; the lane is forgotten at zero.</summary>
        public int Interested { get; set; }
    }

    /// <summary>Ownership of one session lane; disposing releases it exactly once.</summary>
    public sealed class Lease: IDisposable
    {
        private readonly SessionLaneRegistry _registry;
        private readonly (AgentId, SessionId) _key;
        private Lane? _lane;

        internal Lease(SessionLaneRegistry registry, (AgentId, SessionId) key, Lane lane)
        {
            _registry = registry;
            _key = key;
            _lane = lane;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            var lane = Interlocked.Exchange(ref _lane, null);
            if (lane is not null)
            {
                _registry.Release(_key, lane, held: true);
            }
        }
    }
}
