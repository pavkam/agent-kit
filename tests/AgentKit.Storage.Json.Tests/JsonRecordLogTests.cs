// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

using System.Diagnostics;

/// <summary>Verifies newline-framed append, ordered replay, torn-tail reporting, and atomic compaction of one record log.</summary>
/// <remarks>
/// The log's whole value is that an acknowledged record survives process loss and that an unacknowledged one is
/// distinguishable from corruption. These cases therefore assert the exact bytes on disk and the torn-tail flag rather than
/// only the decoded record sequence, because framing is what makes recovery decidable.
/// </remarks>
public sealed class JsonRecordLogTests
{
    private const int _maximumRecordBytes = 32;

    /// <summary>Gets the ambient test cancellation token passed to every log operation that accepts one.</summary>
    /// <value>The running test's token, so a hung case is cancelled rather than blocking the whole run.</value>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Verifies a null path is rejected before the log binds to anything.</summary>
    [Fact]
    public void Constructor_WhenPathIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new JsonRecordLog(null!, _maximumRecordBytes));
        exception.ParamName.ShouldBe("path");
    }

    /// <summary>Verifies a whitespace-only path is rejected as blank rather than bound and probed later.</summary>
    [Fact]
    public void Constructor_WhenPathIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new JsonRecordLog("\t", _maximumRecordBytes));
        exception.ParamName.ShouldBe("path");
    }

    /// <summary>Verifies a zero record bound is rejected, since no framed record could ever satisfy it.</summary>
    [Fact]
    public void Constructor_WhenMaximumRecordBytesIsZero_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JsonRecordLog("/tmp/log.jsonl", 0));
        exception.ParamName.ShouldBe("maximumRecordBytes");
    }

    /// <summary>Verifies a negative record bound is rejected at the boundary just below zero.</summary>
    [Fact]
    public void Constructor_WhenMaximumRecordBytesIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JsonRecordLog("/tmp/log.jsonl", -1));
        exception.ParamName.ShouldBe("maximumRecordBytes");
    }

    /// <summary>Verifies construction binds the exact supplied path without normalizing or creating it.</summary>
    [Fact]
    public void Path_WhenConstructed_ReturnsSuppliedPathWithoutCreatingFile()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");

        var log = new JsonRecordLog(path, _maximumRecordBytes);

        log.Path.ShouldBe(path);
        File.Exists(path).ShouldBeFalse();
    }

    /// <summary>Verifies an unwritten log reports that it does not exist, which is a newly initialized store's state.</summary>
    [Fact]
    public void Exists_WhenLogHasNeverBeenWritten_ReturnsFalse()
    {
        using var directory = new TestTemporaryDirectory();

        var log = new JsonRecordLog(directory.Combine("records.jsonl"), _maximumRecordBytes);

        log.Exists.ShouldBeFalse();
    }

    /// <summary>Verifies the first acknowledged append makes the log observable on disk.</summary>
    [Fact]
    public void Exists_WhenRecordAppended_ReturnsTrue()
    {
        using var directory = new TestTemporaryDirectory();
        var log = new JsonRecordLog(directory.Combine("records.jsonl"), _maximumRecordBytes);

        log.Append(/*lang=json,strict*/ """{"a":1}"""u8.ToArray(), Token);

        log.Exists.ShouldBeTrue();
    }

    /// <summary>Verifies a null record is rejected before the log file is created.</summary>
    [Fact]
    public void Append_WhenRecordIsNull_ThrowsArgumentNullExceptionBeforeWriting()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        var log = new JsonRecordLog(path, _maximumRecordBytes);

        var exception = Should.Throw<ArgumentNullException>(() => log.Append(null!, Token));

        exception.ParamName.ShouldBe("record");
        File.Exists(path).ShouldBeFalse();
    }

    /// <summary>Verifies a record one byte past the bound is rejected before the log file is created.</summary>
    [Fact]
    public void Append_WhenRecordExceedsBound_ThrowsArgumentOutOfRangeExceptionBeforeWriting()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        var log = new JsonRecordLog(path, _maximumRecordBytes);

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => log.Append(new byte[_maximumRecordBytes + 1], Token));

        exception.ParamName.ShouldBe("record");
        File.Exists(path).ShouldBeFalse();
    }

    /// <summary>Verifies a record of exactly the permitted length is accepted, proving the bound is inclusive.</summary>
    [Fact]
    public void Append_WhenRecordLengthEqualsBound_WritesRecord()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        var log = new JsonRecordLog(path, _maximumRecordBytes);
        var record = new byte[_maximumRecordBytes];
        Array.Fill(record, (byte) 'x');

        log.Append(record, Token);

        File.ReadAllBytes(path).ShouldBe([.. record, (byte) '\n']);
    }

    /// <summary>Verifies an embedded newline is refused, because it would silently split one record into two frames.</summary>
    [Fact]
    public void Append_WhenRecordContainsNewline_ThrowsInvalidDataExceptionBeforeWriting()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        var log = new JsonRecordLog(path, _maximumRecordBytes);

        _ = Should.Throw<InvalidDataException>(() => log.Append("a\nb"u8.ToArray(), Token));

        File.Exists(path).ShouldBeFalse();
    }

    /// <summary>Verifies an already cancelled token stops the append before any byte reaches the log.</summary>
    [Fact]
    public void Append_WhenCancelled_ThrowsOperationCanceledExceptionBeforeWriting()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        var log = new JsonRecordLog(path, _maximumRecordBytes);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Should.Throw<OperationCanceledException>(
            () => log.Append(/*lang=json,strict*/ """{"a":1}"""u8.ToArray(), cancellation.Token));

        File.Exists(path).ShouldBeFalse();
    }

    /// <summary>Verifies one append writes exactly the record followed by its single terminating newline.</summary>
    [Fact]
    public void Append_WhenCalledOnce_WritesNewlineFramedRecord()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        var log = new JsonRecordLog(path, _maximumRecordBytes);

        log.Append(/*lang=json,strict*/ """{"a":1}"""u8.ToArray(), Token);

        File.ReadAllText(path).ShouldBe(/*lang=json,strict*/ "{\"a\":1}\n");
    }

    /// <summary>Verifies repeated appends accumulate in issue order rather than overwriting the log.</summary>
    [Fact]
    public void Append_WhenCalledRepeatedly_PreservesDurableAppendOrder()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        var log = new JsonRecordLog(path, _maximumRecordBytes);

        log.Append("first"u8.ToArray(), Token);
        log.Append("second"u8.ToArray(), Token);
        log.Append("third"u8.ToArray(), Token);

        File.ReadAllText(path).ShouldBe("first\nsecond\nthird\n");
    }

    /// <summary>Verifies a missing log replays as empty with no torn tail, which is the correct newly initialized state.</summary>
    [Fact]
    public void Replay_WhenLogIsMissing_ReturnsEmptyWithoutTornTail()
    {
        using var directory = new TestTemporaryDirectory();
        var log = new JsonRecordLog(directory.Combine("records.jsonl"), _maximumRecordBytes);

        var replay = log.Replay(Token);

        replay.Records.ShouldBeEmpty();
        replay.HasIncompleteTrailingRecord.ShouldBeFalse();
    }

    /// <summary>Verifies acknowledged records replay in the exact order they were appended.</summary>
    [Fact]
    public void Replay_WhenRecordsWereAppended_ReturnsThemInAppendOrder()
    {
        using var directory = new TestTemporaryDirectory();
        var log = new JsonRecordLog(directory.Combine("records.jsonl"), _maximumRecordBytes);
        log.Append("first"u8.ToArray(), Token);
        log.Append("second"u8.ToArray(), Token);

        var replay = log.Replay(Token);

        Decode(replay).ShouldBe(["first", "second"]);
        replay.HasIncompleteTrailingRecord.ShouldBeFalse();
    }

    /// <summary>Verifies a log ending on a complete record reports no torn tail.</summary>
    [Fact]
    public void Replay_WhenFileEndsWithNewline_ReportsNoIncompleteTrailingRecord()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        File.WriteAllText(path, "first\nsecond\n");
        var log = new JsonRecordLog(path, _maximumRecordBytes);

        var replay = log.Replay(Token);

        replay.HasIncompleteTrailingRecord.ShouldBeFalse();
        replay.Records.Count.ShouldBe(2);
    }

    /// <summary>Verifies an unterminated final segment is reported separately instead of being replayed as a record.</summary>
    [Fact]
    public void Replay_WhenFileDoesNotEndWithNewline_ReportsIncompleteTrailingRecord()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        File.WriteAllText(path, "first\nsecond\ntor");
        var log = new JsonRecordLog(path, _maximumRecordBytes);

        var replay = log.Replay(Token);

        replay.HasIncompleteTrailingRecord.ShouldBeTrue();
        Decode(replay).ShouldBe(["first", "second"]);
    }

    /// <summary>Verifies empty lines are skipped rather than replayed as zero-length records.</summary>
    [Fact]
    public void Replay_WhenZeroLengthLinesArePresent_SkipsThem()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        File.WriteAllText(path, "\nfirst\n\n\nsecond\n\n");
        var log = new JsonRecordLog(path, _maximumRecordBytes);

        var replay = log.Replay(Token);

        Decode(replay).ShouldBe(["first", "second"]);
        replay.HasIncompleteTrailingRecord.ShouldBeFalse();
    }

    /// <summary>Verifies a framed record past the bound is genuine corruption and is refused rather than truncated.</summary>
    [Fact]
    public void Replay_WhenFramedRecordExceedsBound_ThrowsInvalidDataException()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        File.WriteAllText(path, new string('x', _maximumRecordBytes + 1) + "\n");
        var log = new JsonRecordLog(path, _maximumRecordBytes);

        _ = Should.Throw<InvalidDataException>(() => log.Replay(Token));
    }

    /// <summary>Verifies an already cancelled token stops replay before any record is produced.</summary>
    [Fact]
    public void Replay_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        File.WriteAllText(path, "first\n");
        var log = new JsonRecordLog(path, _maximumRecordBytes);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Should.Throw<OperationCanceledException>(() => log.Replay(cancellation.Token));
    }

    /// <summary>Verifies a null record set is rejected before the existing log is touched.</summary>
    [Fact]
    public void Compact_WhenRecordsAreNull_ThrowsArgumentNullExceptionBeforeWriting()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        var log = new JsonRecordLog(path, _maximumRecordBytes);
        log.Append("first"u8.ToArray(), Token);

        var exception = Should.Throw<ArgumentNullException>(() => log.Compact(null!, Token));

        exception.ParamName.ShouldBe("records");
        File.ReadAllText(path).ShouldBe("first\n");
        directory.TemporaryFiles().ShouldBeEmpty();
    }

    /// <summary>Verifies compaction rewrites the whole log from the supplied authoritative record set.</summary>
    [Fact]
    public void Compact_WhenRecordsSupplied_RewritesLogFromThemWithoutResidue()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        var log = new JsonRecordLog(path, _maximumRecordBytes);
        log.Append("first"u8.ToArray(), Token);
        log.Append("second"u8.ToArray(), Token);

        log.Compact(["second"u8.ToArray(), "third"u8.ToArray()], Token);

        File.ReadAllText(path).ShouldBe("second\nthird\n");
        directory.TemporaryFiles().ShouldBeEmpty();
    }

    /// <summary>Verifies compaction with no records produces an empty but present log rather than deleting it.</summary>
    [Fact]
    public void Compact_WhenRecordsAreEmpty_WritesEmptyLog()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        var log = new JsonRecordLog(path, _maximumRecordBytes);
        log.Append("first"u8.ToArray(), Token);

        log.Compact([], Token);

        log.Exists.ShouldBeTrue();
        log.Replay(Token).Records.ShouldBeEmpty();
    }

    /// <summary>Verifies compaction discards an unacknowledged trailing segment by rewriting from authoritative state.</summary>
    [Fact]
    public void Compact_WhenLogHasTornTail_DropsTheTornTail()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        File.WriteAllText(path, "first\nsecond\ntor");
        var log = new JsonRecordLog(path, _maximumRecordBytes);
        var retained = log.Replay(Token).Records.Select(static record => record.ToArray()).ToArray();

        log.Compact(retained, Token);

        var replay = log.Replay(Token);
        replay.HasIncompleteTrailingRecord.ShouldBeFalse();
        Decode(replay).ShouldBe(["first", "second"]);
    }

    /// <summary>Verifies an unframeable record aborts compaction, preserves the previous log, and leaves no residue.</summary>
    [Fact]
    public void Compact_WhenRecordContainsNewline_ThrowsInvalidDataExceptionAndPreservesLog()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        var log = new JsonRecordLog(path, _maximumRecordBytes);
        log.Append("first"u8.ToArray(), Token);

        _ = Should.Throw<InvalidDataException>(() => log.Compact(["a\nb"u8.ToArray()], Token));

        File.ReadAllText(path).ShouldBe("first\n");
        directory.TemporaryFiles().ShouldBeEmpty();
    }

    /// <summary>Verifies an oversized record aborts compaction, preserves the previous log, and leaves no residue.</summary>
    [Fact]
    public void Compact_WhenRecordExceedsBound_ThrowsInvalidDataExceptionAndPreservesLog()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        var log = new JsonRecordLog(path, _maximumRecordBytes);
        log.Append("first"u8.ToArray(), Token);

        _ = Should.Throw<InvalidDataException>(() => log.Compact([new byte[_maximumRecordBytes + 1]], Token));

        File.ReadAllText(path).ShouldBe("first\n");
        directory.TemporaryFiles().ShouldBeEmpty();
    }

    /// <summary>Verifies an already cancelled token stops compaction before the previous log changes.</summary>
    [Fact]
    public void Compact_WhenCancelled_ThrowsOperationCanceledExceptionAndPreservesLog()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("records.jsonl");
        var log = new JsonRecordLog(path, _maximumRecordBytes);
        log.Append("first"u8.ToArray(), Token);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Should.Throw<OperationCanceledException>(
            () => log.Compact(["second"u8.ToArray()], cancellation.Token));

        File.ReadAllText(path).ShouldBe("first\n");
        directory.TemporaryFiles().ShouldBeEmpty();
    }

    private static string[] Decode(JsonRecordLogReplay replay)
    {
        Debug.Assert(replay.Records is not null, "A replay always reports a record list.");
        return [.. replay.Records.Select(static record => Encoding.UTF8.GetString(record.Span))];
    }
}
