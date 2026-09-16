// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionExecutionLaneProvisionRequest behavior and contracts.</summary>
public sealed class SessionExecutionLaneProvisionRequestTests
{
    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionExecutionLaneProvisionRequest(
            null!, SessionsTestData.Cursor(), new SessionVersion(1), SessionsTestData.EntryId,
            SessionsTestData.ProfileReference(), SessionsTestData.Configuration(), DateTimeOffset.UnixEpoch, new IdempotencyKey("provision")));
        exception.ParamName.ShouldBe("context");
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
    public void Constructor_WhenBranchCursorIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionExecutionLaneProvisionRequest(
            SessionsTestData.BeforeRunContext(), null!, new SessionVersion(1), SessionsTestData.EntryId,
            SessionsTestData.ProfileReference(), SessionsTestData.Configuration(), DateTimeOffset.UnixEpoch, new IdempotencyKey("provision")));
        exception.ParamName.ShouldBe("branchCursor");
    }

    [Fact]
    public void Constructor_WhenEntryIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Request(entryId: default(SessionEntryId)));
        exception.ParamName.ShouldBe("entryId");
    }

    [Fact]
    public void Constructor_WhenSessionProfileIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionExecutionLaneProvisionRequest(
            SessionsTestData.BeforeRunContext(), SessionsTestData.Cursor(), new SessionVersion(1), SessionsTestData.EntryId,
            null!, SessionsTestData.Configuration(), DateTimeOffset.UnixEpoch, new IdempotencyKey("provision")));
        exception.ParamName.ShouldBe("sessionProfile");
    }

    [Fact]
    public void Constructor_WhenConfigurationIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionExecutionLaneProvisionRequest(
            SessionsTestData.BeforeRunContext(), SessionsTestData.Cursor(), new SessionVersion(1), SessionsTestData.EntryId,
            SessionsTestData.ProfileReference(), null!, DateTimeOffset.UnixEpoch, new IdempotencyKey("provision")));
        exception.ParamName.ShouldBe("configuration");
    }

    [Fact]
    public void Constructor_WhenConfigurationVersionDiffersFromAuthorization_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => Request(configurationVersion: new ConfigurationVersion(2)));
        exception.ParamName.ShouldBe("configuration");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionExecutionLaneProvisionRequest(
            SessionsTestData.BeforeRunContext(), SessionsTestData.Cursor(), new SessionVersion(1), SessionsTestData.EntryId,
            SessionsTestData.ProfileReference(), SessionsTestData.Configuration(), DateTimeOffset.UnixEpoch, default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var context = SessionsTestData.BeforeRunContext();
        var request = Request(context: context);
        request.Context.ShouldBe(context);
        request.EntryId.ShouldBe(SessionsTestData.EntryId);
        request.IdempotencyKey.ShouldBe(new IdempotencyKey("provision"));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Request();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SessionExecutionLaneProvisionRequest Request(SessionOperationContext? context = null,
        SessionEntryId? entryId = null, ConfigurationVersion? configurationVersion = null) =>
        new(context ?? SessionsTestData.BeforeRunContext(), SessionsTestData.Cursor(), new SessionVersion(1),
            entryId ?? SessionsTestData.EntryId, SessionsTestData.ProfileReference(),
            SessionsTestData.Configuration(configurationVersion), DateTimeOffset.UnixEpoch,
            new IdempotencyKey("provision"));
}
