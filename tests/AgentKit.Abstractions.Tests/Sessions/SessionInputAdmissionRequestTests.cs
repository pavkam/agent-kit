// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionInputAdmissionRequest behavior and contracts.</summary>
public sealed class SessionInputAdmissionRequestTests
{
    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionInputAdmissionRequest(
            null!, SessionsTestData.AdmissionId, SessionsTestData.EntryId, SessionsTestData.Input(), SessionsTestData.Input(),
            SessionsTestData.Preprocessing(), DateTimeOffset.UnixEpoch, new SessionVersion(1), new SessionLaneRevision(1),
            SessionsTestData.Cursor(), new IdempotencyKey("admit"), 8));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenAdmissionIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Request(admissionId: default(AdmissionId)));
        exception.ParamName.ShouldBe("admissionId");
    }

    [Fact]
    public void Constructor_WhenEntryIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Request(entryId: default(SessionEntryId)));
        exception.ParamName.ShouldBe("entryId");
    }

    [Fact]
    public void Constructor_WhenContextIsNotLaneBound_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => Request(context: SessionsTestData.BeforeRunContext(laneBound: false)));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenContextIsNotBeforeRun_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => Request(context: SessionsTestData.InRunContext()));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenExpectedLaneRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionInputAdmissionRequest(
            SessionsTestData.BeforeRunContext(), SessionsTestData.AdmissionId, SessionsTestData.EntryId, SessionsTestData.Input(),
            SessionsTestData.Input(), SessionsTestData.Preprocessing(), DateTimeOffset.UnixEpoch, new SessionVersion(1),
            default, SessionsTestData.Cursor(), new IdempotencyKey("admit"), 8));
        exception.ParamName.ShouldBe("expectedLaneRevision");
    }

    [Fact]
    public void Constructor_WhenBranchCursorIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionInputAdmissionRequest(
            SessionsTestData.BeforeRunContext(), SessionsTestData.AdmissionId, SessionsTestData.EntryId, SessionsTestData.Input(),
            SessionsTestData.Input(), SessionsTestData.Preprocessing(), DateTimeOffset.UnixEpoch, new SessionVersion(1),
            new SessionLaneRevision(1), null!, new IdempotencyKey("admit"), 8));
        exception.ParamName.ShouldBe("branchCursor");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionInputAdmissionRequest(
            SessionsTestData.BeforeRunContext(), SessionsTestData.AdmissionId, SessionsTestData.EntryId, SessionsTestData.Input(),
            SessionsTestData.Input(), SessionsTestData.Preprocessing(), DateTimeOffset.UnixEpoch, new SessionVersion(1),
            new SessionLaneRevision(1), SessionsTestData.Cursor(), default, 8));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void Constructor_WhenMaximumPendingInputsIsNotPositive_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionInputAdmissionRequest(
            SessionsTestData.BeforeRunContext(), SessionsTestData.AdmissionId, SessionsTestData.EntryId, SessionsTestData.Input(),
            SessionsTestData.Input(), SessionsTestData.Preprocessing(), DateTimeOffset.UnixEpoch, new SessionVersion(1),
            new SessionLaneRevision(1), SessionsTestData.Cursor(), new IdempotencyKey("admit"), 0));
        exception.ParamName.ShouldBe("maximumPendingInputs");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var request = Request();
        request.AdmissionId.ShouldBe(SessionsTestData.AdmissionId);
        request.EntryId.ShouldBe(SessionsTestData.EntryId);
        request.MaximumPendingInputs.ShouldBe(8);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Request();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SessionInputAdmissionRequest Request(SessionOperationContext? context = null, AdmissionId? admissionId = null, SessionEntryId? entryId = null) =>
        new(context ?? SessionsTestData.BeforeRunContext(), admissionId ?? SessionsTestData.AdmissionId, entryId ?? SessionsTestData.EntryId,
            SessionsTestData.Input(), SessionsTestData.Input(), SessionsTestData.Preprocessing(), DateTimeOffset.UnixEpoch,
            new SessionVersion(1), new SessionLaneRevision(1), SessionsTestData.Cursor(), new IdempotencyKey("admit"), 8);
}
