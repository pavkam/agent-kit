// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Creates an isolated durable operation journal and the exact grants it accepts.</summary>
/// <remarks>
/// <para>
/// Journal access is protected, and each adapter recomputes its own enforcement evidence from its journal key and
/// consuming audience. The suite therefore never mints grants itself: it hands each exact request to
/// <c>Authorize</c> and the fixture returns a protected request the adapter under test accepts exactly once. A
/// fixture must not weaken that authorization — issuing a grant with more uses, a wider effect, or an unchecked
/// audience would let a suite pass against an adapter that does not actually enforce anything.
/// </para>
/// <para>
/// Every <c>Authorize</c> call returns a fresh single-use grant, so replaying a previously authorized request is
/// expected to be refused rather than committed twice.
/// </para>
/// </remarks>
public interface IDurableOperationJournalConformanceFixture
{
    /// <summary>Gets the journal key every request this fixture authorizes must target.</summary>
    /// <value>The exact nonblank key the adapter under test was registered with.</value>
    public DurableJournalKey JournalKey { get; }

    /// <summary>Gets which optional journal behaviors the adapter under test supports.</summary>
    /// <value>A declaration whose <see cref="ConformanceCapabilities.SupportsDurability"/> gates reopen cases.</value>
    public ConformanceCapabilities Capabilities { get; }

    /// <summary>Creates the journal under test, isolated from every other scenario.</summary>
    /// <returns>A non-null journal with no recorded operations.</returns>
    /// <remarks>Called once per scenario. Repeated calls within one scenario may return the same instance.</remarks>
    public IDurableOperationJournal CreateJournal();

    /// <summary>Reopens the same durable storage behind a new journal instance.</summary>
    /// <returns>A non-null journal observing every record the previous instance acknowledged.</returns>
    /// <exception cref="NotSupportedException">
    /// The adapter is ephemeral. A fixture that declares
    /// <see cref="ConformanceCapabilities.SupportsDurability"/> as <see langword="false"/> may throw, and the suite
    /// does not call this method.
    /// </exception>
    public IDurableOperationJournal Reopen();

    /// <summary>Wraps one exact acceptance declaration in a fresh single-use grant.</summary>
    /// <param name="start">The exact request the journal will receive.</param>
    /// <returns>A protected request the adapter accepts exactly once.</returns>
    public AuthorizedDurableRequest<DurableOperationStart> Authorize(DurableOperationStart start);

    /// <summary>Wraps one exact state snapshot in a fresh single-use grant.</summary>
    /// <param name="checkpoint">The exact request the journal will receive.</param>
    /// <returns>A protected request the adapter accepts exactly once.</returns>
    public AuthorizedDurableRequest<DurableCheckpoint> Authorize(DurableCheckpoint checkpoint);

    /// <summary>Wraps one exact terminal record in a fresh single-use grant.</summary>
    /// <param name="result">The exact request the journal will receive.</param>
    /// <returns>A protected request the adapter accepts exactly once.</returns>
    public AuthorizedDurableRequest<DurableOperationResult> Authorize(DurableOperationResult result);

    /// <summary>Wraps one exact waiting record in a fresh single-use grant.</summary>
    /// <param name="waiting">The exact request the journal will receive.</param>
    /// <returns>A protected request the adapter accepts exactly once.</returns>
    public AuthorizedDurableRequest<DurableOperationWaiting> Authorize(DurableOperationWaiting waiting);

    /// <summary>Wraps one exact evidence read in a fresh single-use unfenced grant.</summary>
    /// <param name="address">The operation coordinates the read targets.</param>
    /// <returns>A protected request the adapter accepts exactly once.</returns>
    /// <remarks>An evidence read is never fenced, so the returned intent must require no fence.</remarks>
    public AuthorizedDurableRequest<DurableOperationAddress> Authorize(DurableOperationAddress address);
}
