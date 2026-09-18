// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies whole-document reading and atomic replacement, including bounds, cancellation, and residue cleanup.</summary>
/// <remarks>
/// The contract these cases protect is that a reader never observes a partially written document and that no failure path
/// leaves a sibling temporary file behind. Both are asserted directly against the filesystem rather than inferred from a
/// return value, because residue and torn content are exactly what a caller cannot see through the API.
/// </remarks>
public sealed class JsonAtomicDocumentTests
{
    /// <summary>Gets the ambient test cancellation token passed to every replacement that accepts one.</summary>
    /// <value>The running test's token, so a hung case is cancelled rather than blocking the whole run.</value>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Verifies a null path is rejected before any filesystem work is attempted.</summary>
    [Fact]
    public void Read_WhenPathIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonAtomicDocument.Read(null!, 16));
        exception.ParamName.ShouldBe("path");
    }

    /// <summary>Verifies a whitespace-only path is rejected as a blank path rather than probed on disk.</summary>
    [Fact]
    public void Read_WhenPathIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => JsonAtomicDocument.Read("   ", 16));
        exception.ParamName.ShouldBe("path");
    }

    /// <summary>Verifies a zero byte bound is rejected, since no document can satisfy it.</summary>
    [Fact]
    public void Read_WhenMaximumBytesIsZero_ThrowsArgumentOutOfRangeException()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("document.json");
        File.WriteAllBytes(path, "{}"u8.ToArray());

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => JsonAtomicDocument.Read(path, 0));

        exception.ParamName.ShouldBe("maximumBytes");
    }

    /// <summary>Verifies a negative byte bound is rejected at the boundary just below zero.</summary>
    [Fact]
    public void Read_WhenMaximumBytesIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => JsonAtomicDocument.Read("/tmp/absent", -1));
        exception.ParamName.ShouldBe("maximumBytes");
    }

    /// <summary>Verifies a missing document reads as absent rather than as an error, which is the correct new-store state.</summary>
    [Fact]
    public void Read_WhenDocumentIsMissing_ReturnsNull()
    {
        using var directory = new TestTemporaryDirectory();

        var payload = JsonAtomicDocument.Read(directory.Combine("absent.json"), 1024);

        payload.ShouldBeNull();
    }

    /// <summary>Verifies a missing parent directory also reads as absent rather than surfacing a directory error.</summary>
    [Fact]
    public void Read_WhenDirectoryIsMissing_ReturnsNull()
    {
        using var directory = new TestTemporaryDirectory();

        var payload = JsonAtomicDocument.Read(Path.Combine(directory.Path, "absent", "document.json"), 1024);

        payload.ShouldBeNull();
    }

    /// <summary>Verifies the exact stored bytes are returned without truncation, padding, or re-encoding.</summary>
    [Fact]
    public void Read_WhenDocumentExists_ReturnsExactBytes()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("document.json");
        var expected = /*lang=json,strict*/ """{"alpha":1}"""u8.ToArray();
        File.WriteAllBytes(path, expected);

        var payload = JsonAtomicDocument.Read(path, 1024);

        payload.ShouldBe(expected);
    }

    /// <summary>Verifies a document of exactly the permitted length is accepted, proving the bound is inclusive.</summary>
    [Fact]
    public void Read_WhenDocumentLengthEqualsMaximumBytes_ReturnsPayload()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("document.json");
        var expected = "{}"u8.ToArray();
        File.WriteAllBytes(path, expected);

        var payload = JsonAtomicDocument.Read(path, expected.Length);

        payload.ShouldBe(expected);
    }

    /// <summary>Verifies an oversized document is rejected by length before any byte is decoded.</summary>
    [Fact]
    public void Read_WhenDocumentExceedsMaximumBytes_ThrowsInvalidDataException()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("document.json");
        File.WriteAllBytes(path, "{}"u8.ToArray());

        _ = Should.Throw<InvalidDataException>(() => JsonAtomicDocument.Read(path, 1));
    }

    /// <summary>Verifies a null path is rejected before a temporary file is created.</summary>
    [Fact]
    public void Replace_WhenPathIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonAtomicDocument.Replace(null!, [1, 2, 3], Token));
        exception.ParamName.ShouldBe("path");
    }

    /// <summary>Verifies a blank path is rejected and leaves no residue anywhere the replacement would have written.</summary>
    [Fact]
    public void Replace_WhenPathIsBlank_ThrowsArgumentExceptionBeforeWriting()
    {
        using var directory = new TestTemporaryDirectory();

        var exception = Should.Throw<ArgumentException>(() => JsonAtomicDocument.Replace(" ", [1, 2, 3], Token));

        exception.ParamName.ShouldBe("path");
        directory.TemporaryFiles().ShouldBeEmpty();
    }

    /// <summary>Verifies a null payload is rejected before the target or any temporary file is touched.</summary>
    [Fact]
    public void Replace_WhenPayloadIsNull_ThrowsArgumentNullExceptionBeforeWriting()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("document.json");

        var exception = Should.Throw<ArgumentNullException>(() => JsonAtomicDocument.Replace(path, null!, Token));

        exception.ParamName.ShouldBe("payload");
        File.Exists(path).ShouldBeFalse();
        directory.TemporaryFiles().ShouldBeEmpty();
    }

    /// <summary>Verifies a first replacement creates the document with the exact payload and leaves no residue.</summary>
    [Fact]
    public void Replace_WhenTargetIsMissing_CreatesDocumentWithoutResidue()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("document.json");
        var payload = /*lang=json,strict*/ """{"created":true}"""u8.ToArray();

        JsonAtomicDocument.Replace(path, payload, Token);

        File.ReadAllBytes(path).ShouldBe(payload);
        directory.TemporaryFiles().ShouldBeEmpty();
    }

    /// <summary>Verifies a later replacement fully supersedes the previous document rather than merging or appending.</summary>
    [Fact]
    public void Replace_WhenTargetExists_OverwritesWithCompleteNewDocument()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("document.json");
        JsonAtomicDocument.Replace(path, /*lang=json,strict*/ """{"generation":1,"padding":"aaaaaaaaaa"}"""u8.ToArray(), Token);
        var replacement = /*lang=json,strict*/ """{"generation":2}"""u8.ToArray();

        JsonAtomicDocument.Replace(path, replacement, Token);

        File.ReadAllBytes(path).ShouldBe(replacement);
        directory.TemporaryFiles().ShouldBeEmpty();
    }

    /// <summary>Verifies an already cancelled token stops the replacement before the previous document changes.</summary>
    [Fact]
    public void Replace_WhenCancelledBeforeRename_LeavesPreviousDocumentIntact()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("document.json");
        var original = /*lang=json,strict*/ """{"generation":1}"""u8.ToArray();
        JsonAtomicDocument.Replace(path, original, Token);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Should.Throw<OperationCanceledException>(
            () => JsonAtomicDocument.Replace(path, /*lang=json,strict*/ """{"generation":2}"""u8.ToArray(), cancellation.Token));

        File.ReadAllBytes(path).ShouldBe(original);
        directory.TemporaryFiles().ShouldBeEmpty();
    }

    /// <summary>Verifies a failed rename removes the temporary file so a retry is never blocked by residue.</summary>
    /// <remarks>
    /// Targeting an existing directory makes the rename fail deterministically after the temporary file has already been
    /// written and flushed, which is the only failure path where residue could survive.
    /// </remarks>
    [Fact]
    public void Replace_WhenRenameFails_RemovesTemporaryFile()
    {
        using var directory = new TestTemporaryDirectory();
        var occupied = directory.Combine("occupied");
        _ = Directory.CreateDirectory(occupied);

        _ = Should.Throw<IOException>(() => JsonAtomicDocument.Replace(occupied, "{}"u8.ToArray(), Token));

        directory.TemporaryFiles().ShouldBeEmpty();
        Directory.Exists(occupied).ShouldBeTrue();
    }

    /// <summary>Verifies removing an absent file is a silent no-op, so cleanup never masks an originating failure.</summary>
    [Fact]
    public void TryDelete_WhenFileIsMissing_DoesNotThrow()
    {
        using var directory = new TestTemporaryDirectory();

        Should.NotThrow(() => JsonAtomicDocument.TryDelete(directory.Combine("absent.tmp")));
    }

    /// <summary>Verifies an existing temporary file is actually removed rather than only ignored.</summary>
    [Fact]
    public void TryDelete_WhenFileExists_RemovesIt()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("residue.tmp");
        File.WriteAllBytes(path, "{}"u8.ToArray());

        JsonAtomicDocument.TryDelete(path);

        File.Exists(path).ShouldBeFalse();
    }

    /// <summary>Verifies an unremovable path is swallowed, because best-effort cleanup must never replace the real error.</summary>
    [Fact]
    public void TryDelete_WhenPathIsDirectory_DoesNotThrow()
    {
        using var directory = new TestTemporaryDirectory();
        var path = directory.Combine("directory.tmp");
        _ = Directory.CreateDirectory(path);

        Should.NotThrow(() => JsonAtomicDocument.TryDelete(path));

        Directory.Exists(path).ShouldBeTrue();
    }
}
