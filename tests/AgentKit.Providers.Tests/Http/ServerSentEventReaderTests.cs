// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Http;

using System.Text;

using AgentKit.Providers.Http;
using AgentKit.TestSupport;

/// <summary>Verifies the shared server-sent-event reader against the WHATWG event-stream processing model at arbitrary chunk boundaries.</summary>
public sealed class ServerSentEventReaderTests
{
    /// <summary>Read sizes that place chunk boundaries inside field names, after colons, inside CR LF pairs, and inside multi-byte sequences.</summary>
    public static TheoryData<int> ChunkSizes => [1, 2, 3, 7, 4096];

    [Fact]
    public void ReadAsync_WhenStreamIsNull_ThrowsBeforeEnumeration()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ServerSentEventReader.ReadAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("stream");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ReadAsync_WhenSingleDataLineEvent_YieldsPayloadWithDefaultType(int chunkSize)
    {
        var events = await ReadAllAsync("data: {\"a\":1}\n\n", chunkSize);

        var single = events.ShouldHaveSingleItem();
        single.Data.ShouldBe(/*lang=json,strict*/ "{\"a\":1}");
        single.Event.ShouldBeNull();
        single.Id.ShouldBeNull();
        single.Retry.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ReadAsync_WhenEventHasSeveralDataLines_JoinsThemWithLineFeed(int chunkSize)
    {
        var events = await ReadAllAsync("data: first\ndata: second\ndata: third\n\n", chunkSize);

        events.ShouldHaveSingleItem().Data.ShouldBe("first\nsecond\nthird");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ReadAsync_WhenBlankLinesSeparateEvents_DispatchesEachEventInOrder(int chunkSize)
    {
        var events = await ReadAllAsync("data: one\n\ndata: two\n\ndata: three\n\n", chunkSize);

        events.Select(e => e.Data).ShouldBe(["one", "two", "three"]);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ReadAsync_WhenFinalEventLacksTrailingBlankLine_DispatchesItAtEndOfStream(int chunkSize)
    {
        var events = await ReadAllAsync("data: one\n\ndata: two", chunkSize);

        events.Select(e => e.Data).ShouldBe(["one", "two"]);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ReadAsync_WhenLineEndingsAreCrLf_ParsesEveryEvent(int chunkSize)
    {
        var events = await ReadAllAsync("data: one\r\n\r\ndata: two\r\ndata: more\r\n\r\n", chunkSize);

        events.Select(e => e.Data).ShouldBe(["one", "two\nmore"]);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ReadAsync_WhenLineEndingsAreBareCr_ParsesEveryEvent(int chunkSize)
    {
        var events = await ReadAllAsync("data: one\r\rdata: two\r\r", chunkSize);

        events.Select(e => e.Data).ShouldBe(["one", "two"]);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ReadAsync_WhenLineEndingsAreMixed_ParsesEveryEvent(int chunkSize)
    {
        var events = await ReadAllAsync("data: one\r\n\ndata: two\n\r\ndata: three\r\r", chunkSize);

        events.Select(e => e.Data).ShouldBe(["one", "two", "three"]);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ReadAsync_WhenCommentLinesArePresent_IgnoresThem(int chunkSize)
    {
        var events = await ReadAllAsync(": keepalive\n\ndata: one\n: mid-event comment\ndata: two\n\n:\n\n", chunkSize);

        events.ShouldHaveSingleItem().Data.ShouldBe("one\ntwo");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ReadAsync_WhenEventFieldIsPresent_ExposesTypeAndResetsItAfterDispatch(int chunkSize)
    {
        var events = await ReadAllAsync("event: message_start\ndata: a\n\ndata: b\n\n", chunkSize);

        events.Count.ShouldBe(2);
        events[0].Event.ShouldBe("message_start");
        events[0].Data.ShouldBe("a");
        events[1].Event.ShouldBeNull();
        events[1].Data.ShouldBe("b");
    }

    [Fact]
    public async Task ReadAsync_WhenEventFieldRepeats_LastValueWins()
    {
        var events = await ReadAllAsync("event: first\nevent: second\ndata: a\n\n", 4096);

        events.ShouldHaveSingleItem().Event.ShouldBe("second");
    }

    [Fact]
    public async Task ReadAsync_WhenEventFieldIsEmpty_ExposesDefaultTypeAsNull()
    {
        var events = await ReadAllAsync("event:\ndata: a\n\n", 4096);

        events.ShouldHaveSingleItem().Event.ShouldBeNull();
    }

    [Fact]
    public async Task ReadAsync_WhenBlankLineArrivesWithoutData_DiscardsPendingEventTypeWithoutDispatching()
    {
        var events = await ReadAllAsync("event: orphan\n\ndata: a\n\n", 4096);

        var single = events.ShouldHaveSingleItem();
        single.Event.ShouldBeNull();
        single.Data.ShouldBe("a");
    }

    [Fact]
    public async Task ReadAsync_WhenStreamEndsWithEventTypeButNoData_DispatchesNothing()
    {
        var events = await ReadAllAsync("data: a\n\nevent: trailing\nid: 9\n", 4096);

        events.ShouldHaveSingleItem().Data.ShouldBe("a");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ReadAsync_WhenIdFieldIsPresent_PersistsLastEventIdAcrossLaterEvents(int chunkSize)
    {
        var events = await ReadAllAsync("data: a\n\nid: 42\ndata: b\n\ndata: c\n\nid: 43\ndata: d\n\n", chunkSize);

        events.Select(e => e.Id).ShouldBe([null, "42", "42", "43"]);
    }

    [Fact]
    public async Task ReadAsync_WhenIdFieldIsEmpty_ResetsLastEventIdToEmptyString()
    {
        var events = await ReadAllAsync("id: 1\ndata: a\n\nid:\ndata: b\n\n", 4096);

        events.Select(e => e.Id).ShouldBe(["1", string.Empty]);
    }

    [Fact]
    public async Task ReadAsync_WhenIdFieldContainsNull_IgnoresThatIdField()
    {
        var events = await ReadAllAsync("id: 1\ndata: a\n\nid: bad\0id\ndata: b\n\n", 4096);

        events.Select(e => e.Id).ShouldBe(["1", "1"]);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ReadAsync_WhenRetryFieldIsAllDigits_PersistsReconnectionTimeAcrossLaterEvents(int chunkSize)
    {
        var events = await ReadAllAsync("retry: 2500\ndata: a\n\ndata: b\n\n", chunkSize);

        events.Select(e => e.Retry).ShouldBe([2500, 2500]);
    }

    [Theory]
    [InlineData("retry: 25ms")]
    [InlineData("retry: -1")]
    [InlineData("retry:")]
    [InlineData("retry: 99999999999")]
    public async Task ReadAsync_WhenRetryFieldIsNotAValidInteger_IgnoresIt(string retryLine)
    {
        var events = await ReadAllAsync($"{retryLine}\ndata: a\n\n", 4096);

        events.ShouldHaveSingleItem().Retry.ShouldBeNull();
    }

    [Fact]
    public async Task ReadAsync_WhenFieldValueHasOneLeadingSpace_StripsExactlyOneSpace()
    {
        var events = await ReadAllAsync("data:no-space\n\ndata: one-space\n\ndata:  two-spaces\n\n", 4096);

        events.Select(e => e.Data).ShouldBe(["no-space", "one-space", " two-spaces"]);
    }

    [Fact]
    public async Task ReadAsync_WhenLineHasNoColon_TreatsWholeLineAsFieldNameWithEmptyValue()
    {
        var events = await ReadAllAsync("data\n\nevent\ndata: x\n\n", 4096);

        events.Count.ShouldBe(2);
        events[0].Data.ShouldBe(string.Empty);
        events[1].Event.ShouldBeNull();
        events[1].Data.ShouldBe("x");
    }

    [Fact]
    public async Task ReadAsync_WhenDataFieldIsEmpty_DispatchesEventWithEmptyPayload()
    {
        var events = await ReadAllAsync("data:\n\n", 4096);

        events.ShouldHaveSingleItem().Data.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ReadAsync_WhenDataValueContainsColons_KeepsEverythingAfterFirstColon()
    {
        var events = await ReadAllAsync("data: {\"t\":\"12:30:00\"}\n\n", 4096);

        events.ShouldHaveSingleItem().Data.ShouldBe(/*lang=json,strict*/ "{\"t\":\"12:30:00\"}");
    }

    [Fact]
    public async Task ReadAsync_WhenFieldNameIsUnknown_IgnoresIt()
    {
        var events = await ReadAllAsync("custom: value\nData: capitalized-is-unknown\ndata: a\n\n", 4096);

        events.ShouldHaveSingleItem().Data.ShouldBe("a");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ReadAsync_WhenMultibyteUtf8SplitAcrossChunks_ReassemblesText(int chunkSize)
    {
        const string text = "héllo 👋 世界 — ok ✅";

        var events = await ReadAllAsync($"data: {text}\n\nevent: 世界\ndata: {text}\n\n", chunkSize);

        events.Count.ShouldBe(2);
        events[0].Data.ShouldBe(text);
        events[1].Event.ShouldBe("世界");
        events[1].Data.ShouldBe(text);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ReadAsync_WhenStreamStartsWithByteOrderMark_IgnoresIt(int chunkSize)
    {
        var payload = Encoding.UTF8.GetPreamble().Concat("data: a\n\n"u8.ToArray()).ToArray();
        await using var stream = new ChunkedStream(payload, chunkSize);

        var events = await ServerSentEventReader.ReadAsync(stream, TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);

        events.ShouldHaveSingleItem().Data.ShouldBe("a");
    }

    [Fact]
    public async Task ReadAsync_WhenStreamIsEmpty_YieldsNothing()
    {
        var events = await ReadAllAsync(string.Empty, 4096);

        events.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadAsync_WhenStreamIsOnlyBlankLinesAndComments_YieldsNothing()
    {
        var events = await ReadAllAsync("\n\n: ping\n\n\r\n", 4096);

        events.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadAsync_WhenEnumerationCompletes_LeavesStreamOpen()
    {
        await using var stream = new MemoryStream("data: a\n\n"u8.ToArray());

        _ = await ServerSentEventReader.ReadAsync(stream, TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);

        stream.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadAsync_WhenConsumerBreaksEarly_LeavesStreamOpenAndStopsReading()
    {
        await using var stream = new MemoryStream("data: a\n\ndata: b\n\ndata: c\n\n"u8.ToArray());
        var seen = new List<string>();

        await foreach (var streamEvent in ServerSentEventReader.ReadAsync(stream, TestContext.Current.CancellationToken))
        {
            seen.Add(streamEvent.Data);
            if (streamEvent.Data == "b")
            {
                break;
            }
        }

        seen.ShouldBe(["a", "b"]);
        stream.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadAsync_WhenTokenIsAlreadyCanceled_ThrowsOperationCanceledOnFirstMoveNext()
    {
        await using var stream = new MemoryStream("data: a\n\n"u8.ToArray());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in ServerSentEventReader.ReadAsync(stream, cancellation.Token))
            {
            }
        });
    }

    [Fact]
    public async Task ReadAsync_WhenCanceledWhileAwaitingRead_ThrowsOperationCanceledWithoutYielding()
    {
        var gate = new GatedReadStream();
        using var cancellation = new CancellationTokenSource();
        var yielded = false;

        var reading = Task.Run(async () =>
        {
            await foreach (var _ in ServerSentEventReader.ReadAsync(gate, cancellation.Token))
            {
                yielded = true;
            }
        }, TestContext.Current.CancellationToken);

        await gate.Entered.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => reading);
        yielded.ShouldBeFalse();
    }

    [Fact]
    public async Task ReadAsync_WhenCancellationIsSuppliedThroughWithCancellation_HonorsIt()
    {
        var gate = new GatedReadStream();
        using var cancellation = new CancellationTokenSource();

        var reading = Task.Run(async () =>
        {
            await foreach (var _ in ServerSentEventReader.ReadAsync(gate).WithCancellation(cancellation.Token))
            {
            }
        }, TestContext.Current.CancellationToken);

        await gate.Entered.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => reading);
    }

    private static async Task<List<ServerSentEvent>> ReadAllAsync(string body, int chunkSize)
    {
        await using var stream = new ChunkedStream(Encoding.UTF8.GetBytes(body), chunkSize);
        return await ServerSentEventReader.ReadAsync(stream, TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);
    }
}
