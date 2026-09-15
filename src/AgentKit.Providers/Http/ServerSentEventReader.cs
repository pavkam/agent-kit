// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Http;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

/// <summary>
/// Reads a <c>text/event-stream</c> response body into a sequence of
/// dispatched <see cref="ServerSentEvent"/> values according to the WHATWG
/// server-sent events processing model.
/// </summary>
/// <remarks>
/// <para>
/// Every first-party streaming provider adapter shares this reader so the
/// wire-level rules are implemented once and identically:
/// </para>
/// <list type="bullet">
/// <item><description>
/// The body is decoded as UTF-8 through a <see cref="StreamReader"/>, so a
/// multi-byte sequence split across transport reads is reassembled correctly
/// regardless of chunk boundaries. A leading UTF-8 byte order mark is
/// consumed and ignored.
/// </description></item>
/// <item><description>
/// Lines end at U+000D CARRIAGE RETURN, U+000A LINE FEED, or the CR LF
/// pair, including a pair split across reads.
/// </description></item>
/// <item><description>
/// A line starting with U+003A COLON is a comment and is ignored. Otherwise
/// the field name is the text before the first colon (or the whole line when
/// there is none, in which case the value is empty), and exactly one space
/// following the colon is stripped from the value; a second space is
/// content.
/// </description></item>
/// <item><description>
/// <c>event:</c> sets the event type. <c>data:</c> appends its value to the
/// data buffer; several <c>data:</c> lines in one event are joined by a
/// single U+000A. <c>id:</c> sets the last event ID unless the value
/// contains U+0000 NULL. <c>retry:</c> sets the reconnection time when the
/// value is entirely ASCII digits that fit in an <see cref="int"/>. Any
/// other field name is ignored.
/// </description></item>
/// <item><description>
/// An empty line dispatches the pending event when at least one
/// <c>data:</c> field was received (an event whose only <c>data:</c> field
/// carried an empty value is still dispatched, with empty
/// <see cref="ServerSentEvent.Data"/>). An empty line with no pending data
/// discards the pending event type without dispatching anything. A final
/// event that reaches end of stream without a terminating empty line is
/// dispatched when it has data.
/// </description></item>
/// </list>
/// <para>
/// The reader never interprets the payload: dialect sentinels such as the
/// OpenAI <c>[DONE]</c> marker, JSON decoding, and routing on the event type
/// belong to the consuming adapter. The reader owns no stream: the caller
/// retains ownership of <see cref="Stream"/> and the reader leaves it open
/// when enumeration completes, breaks early, or fails.
/// </para>
/// <para>
/// This type is stateless and safe to call concurrently; each enumeration
/// carries its own buffers. Cancellation is observed at every read of the
/// underlying stream and surfaces as <see cref="OperationCanceledException"/>
/// from the enumerator.
/// </para>
/// </remarks>
public static class ServerSentEventReader
{
    private const string _eventField = "event";
    private const string _dataField = "data";
    private const string _idField = "id";
    private const string _retryField = "retry";

    /// <summary>
    /// Reads dispatched server-sent events from <paramref name="stream"/>
    /// until end of stream.
    /// </summary>
    /// <param name="stream">The readable <c>text/event-stream</c> body. The caller retains ownership; it is left open.</param>
    /// <param name="cancellationToken">Cancels the enumeration at the next read of <paramref name="stream"/>.</param>
    /// <returns>
    /// A lazily evaluated sequence yielding one <see cref="ServerSentEvent"/>
    /// per dispatched event in wire order. Enumeration begins reading only
    /// when the sequence is iterated, and stopping iteration early leaves
    /// the remainder of <paramref name="stream"/> unread.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown by the enumerator when <paramref name="cancellationToken"/> is canceled.</exception>
    public static IAsyncEnumerable<ServerSentEvent> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return ReadCoreAsync(stream, cancellationToken);
    }

    private static async IAsyncEnumerable<ServerSentEvent> ReadCoreAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Debug.Assert(stream is not null, "The public entry point validates the stream before deferring enumeration.");

        // Encoding.UTF8 carries a preamble, so the reader consumes a leading byte order mark even with automatic
        // detection disabled; detection stays off because the specification mandates UTF-8 regardless of any BOM.
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

        var data = new StringBuilder();
        var hasData = false;
        string? eventType = null;
        string? lastEventId = null;
        int? retry = null;

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                if (hasData)
                {
                    yield return new ServerSentEvent(NormalizeEventType(eventType), data.ToString(), lastEventId, retry);
                }

                _ = data.Clear();
                hasData = false;
                eventType = null;
                continue;
            }

            if (line[0] == ':')
            {
                continue;
            }

            var (field, value) = SplitField(line);
            switch (field)
            {
                case _eventField:
                    eventType = value;
                    break;

                case _dataField:
                    if (hasData)
                    {
                        _ = data.Append('\n');
                    }

                    _ = data.Append(value);
                    hasData = true;
                    break;

                case _idField:
                    if (!value.Contains('\0'))
                    {
                        lastEventId = value;
                    }

                    break;

                case _retryField:
                    if (TryParseReconnectionTime(value, out var reconnectionTime))
                    {
                        retry = reconnectionTime;
                    }

                    break;

                default:
                    break;
            }
        }

        if (hasData)
        {
            yield return new ServerSentEvent(NormalizeEventType(eventType), data.ToString(), lastEventId, retry);
        }
    }

    /// <summary>Splits one non-empty, non-comment line into its field name and value per the specification.</summary>
    /// <param name="line">The line to split.</param>
    /// <returns>
    /// The field name and value. Without a colon the whole line is the name
    /// and the value is empty; otherwise the value is the text after the
    /// first colon with exactly one leading space removed when present.
    /// </returns>
    private static (string Field, string Value) SplitField(string line)
    {
        Debug.Assert(line.Length > 0, "Callers dispatch empty lines before splitting.");
        Debug.Assert(line[0] != ':', "Callers discard comment lines before splitting.");

        var colon = line.IndexOf(':');
        if (colon < 0)
        {
            return (line, string.Empty);
        }

        var valueStart = colon + 1;
        if (valueStart < line.Length && line[valueStart] == ' ')
        {
            valueStart++;
        }

        return (line[..colon], line[valueStart..]);
    }

    /// <summary>Parses a <c>retry:</c> value, which the specification honors only when it is entirely ASCII digits.</summary>
    /// <param name="value">The field value.</param>
    /// <param name="reconnectionTime">The parsed milliseconds when the method returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="value"/> is a non-empty
    /// run of ASCII digits that fits in an <see cref="int"/>; otherwise
    /// <see langword="false"/> and the field is ignored.
    /// </returns>
    private static bool TryParseReconnectionTime(string value, out int reconnectionTime)
    {
        Debug.Assert(value is not null, "Field splitting never yields a null value.");

        reconnectionTime = 0;
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out reconnectionTime);
    }

    /// <summary>Maps the event type buffer to the public shape: an empty or unset buffer is the default type and surfaces as null.</summary>
    /// <param name="eventType">The event type buffer at dispatch.</param>
    /// <returns>The buffer when it names a type; otherwise <see langword="null"/>.</returns>
    private static string? NormalizeEventType(string? eventType) => eventType is { Length: > 0 } ? eventType : null;
}
