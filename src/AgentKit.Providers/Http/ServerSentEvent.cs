// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Http;

/// <summary>
/// One dispatched server-sent event as produced by
/// <see cref="ServerSentEventReader"/>: the event type, the spec-joined data
/// payload, and the stream-level last-event-id and reconnection-time state
/// in effect when the event was dispatched.
/// </summary>
/// <remarks>
/// <para>
/// The shape follows the WHATWG <c>text/event-stream</c> processing model.
/// <see cref="Event"/> and <see cref="Data"/> are per-event buffers that
/// reset after every dispatch. <see cref="Id"/> and <see cref="Retry"/>
/// mirror the event source's persistent "last event ID" and "reconnection
/// time" state: once a stream sets them they remain in effect for every
/// later event until the stream changes them again, so consecutive events
/// may legitimately report the same <see cref="Id"/>.
/// </para>
/// <para>
/// The payload is opaque text. The reader does not interpret dialect
/// sentinels such as the OpenAI <c>[DONE]</c> marker, parse JSON, or trim
/// anything beyond the single optional space the specification strips after
/// a field's colon; those decisions belong to the protocol adapter that
/// consumes the events.
/// </para>
/// </remarks>
public sealed record ServerSentEvent
{
    /// <summary>Initializes a new instance of the <see cref="ServerSentEvent"/> record.</summary>
    /// <param name="eventType">
    /// The value of the last <c>event:</c> field received for this event, or
    /// <see langword="null"/> when the stream did not name a type (the
    /// specification's default <c>message</c> type) or named it with an
    /// empty value.
    /// </param>
    /// <param name="data">
    /// The event payload: every <c>data:</c> field value of the event in
    /// order, joined by a single U+000A LINE FEED. May be empty when the
    /// event's only <c>data:</c> field carried no value.
    /// </param>
    /// <param name="lastEventId">
    /// The stream's last event ID string at dispatch time, or
    /// <see langword="null"/> when no <c>id:</c> field has been received on
    /// the stream so far.
    /// </param>
    /// <param name="retry">
    /// The stream's reconnection time in milliseconds at dispatch time, or
    /// <see langword="null"/> when no valid <c>retry:</c> field has been
    /// received on the stream so far.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="retry"/> is negative.</exception>
    public ServerSentEvent(string? eventType, string data, string? lastEventId, int? retry)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (retry is { } reconnectionTime)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(reconnectionTime, nameof(retry));
        }

        Event = eventType;
        Data = data;
        Id = lastEventId;
        Retry = retry;
    }

    /// <summary>Gets the event type named by the last <c>event:</c> field of this event.</summary>
    /// <value>
    /// The type name, or <see langword="null"/> for an event whose type was
    /// not named or was named with an empty value, which the specification
    /// treats as the default <c>message</c> type.
    /// </value>
    public string? Event { get; }

    /// <summary>Gets the event payload assembled from every <c>data:</c> field of the event.</summary>
    /// <value>
    /// The field values in wire order joined by a single U+000A LINE FEED,
    /// with no trailing line feed. Empty when the event's only <c>data:</c>
    /// field carried no value. Never <see langword="null"/>.
    /// </value>
    public string Data { get; }

    /// <summary>Gets the stream's last event ID string in effect when this event was dispatched.</summary>
    /// <value>
    /// The most recent <c>id:</c> value that did not contain U+0000 NULL, or
    /// <see langword="null"/> when the stream has not yet set one. The value
    /// persists across events until the stream changes it.
    /// </value>
    public string? Id { get; }

    /// <summary>Gets the stream's reconnection time in effect when this event was dispatched.</summary>
    /// <value>
    /// The most recent all-digit <c>retry:</c> value in milliseconds, or
    /// <see langword="null"/> when the stream has not yet set a valid one.
    /// The value persists across events until the stream changes it.
    /// </value>
    public int? Retry { get; }
}
