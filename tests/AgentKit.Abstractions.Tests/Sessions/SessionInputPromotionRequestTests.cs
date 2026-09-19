// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionInputPromotionRequest behavior and contracts.</summary>
public sealed class SessionInputPromotionRequestTests
{
    [Fact]
    public void Constructor_WhenContextIsNotInRun_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => Request(SessionsTestData.BeforeRunContext()));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenContextHasNoTurn_ThrowsExactArgumentException()
    {
        var correlation = new InRunOperationCorrelation(SessionsTestData.OperationId, SessionsTestData.RunId, null);
        var context = new SessionOperationContext(
            SessionsTestData.AgentId, SessionsTestData.SessionId, SessionsTestData.LaneId, correlation,
            SessionsTestData.Identity(), SessionsTestData.Authorization(correlation, SessionsTestData.SessionId));
        var exception = Should.Throw<ArgumentException>(() => Request(context));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenSelectedAdmissionIdsIsEmpty_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => Request(SessionsTestData.InRunContext(), selected: []));
        exception.ParamName.ShouldBe("selectedAdmissionIds");
    }

    [Fact]
    public void Constructor_WhenEntryIdsCountDiffersFromSelection_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => Request(
            SessionsTestData.InRunContext(), entryIds: [Id<SessionEntryId>(1), Id<SessionEntryId>(2)]));
        exception.ParamName.ShouldBe("entryIds");
    }

    [Fact]
    public void Constructor_WhenPromotionEntryIdIsAlsoAnEntryId_ThrowsExactArgumentException()
    {
        var promotionEntryId = Id<SessionEntryId>(9);
        var exception = Should.Throw<ArgumentException>(() => Request(
            SessionsTestData.InRunContext(), promotionEntryId: promotionEntryId, entryIds: [promotionEntryId]));
        exception.ParamName.ShouldBe("entryIds");
    }

    [Fact]
    public void Constructor_WhenExpectedStateRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => Request(SessionsTestData.InRunContext(), expectedStateRevision: default(OperationStateRevision)));
        exception.ParamName.ShouldBe("expectedStateRevision");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => Request(SessionsTestData.InRunContext(), idempotencyKey: default(IdempotencyKey)));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesDerivedProperties()
    {
        var context = SessionsTestData.InRunContext();
        var request = Request(context);

        request.Context.ShouldBe(context);
        request.RunId.ShouldBe(SessionsTestData.RunId);
        request.TargetTurnId.ShouldBe(SessionsTestData.TurnId);
        request.SelectedAdmissionIds.ShouldBe([SessionsTestData.AdmissionId]);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Request(SessionsTestData.InRunContext());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SessionInputPromotionRequest Request(
        SessionOperationContext context,
        ImmutableArray<AdmissionId>? selected = null,
        SessionEntryId? promotionEntryId = null,
        ImmutableArray<SessionEntryId>? entryIds = null,
        OperationStateRevision? expectedStateRevision = null,
        IdempotencyKey? idempotencyKey = null) =>
        new(
            context,
            selected ?? [SessionsTestData.AdmissionId],
            new SessionSequence(1),
            new SessionLaneRevision(1),
            new SessionVersion(1),
            SessionsTestData.Cursor(),
            promotionEntryId ?? Id<SessionEntryId>(20),
            entryIds ?? [Id<SessionEntryId>(21)],
            [Id<MessageId>(22)],
            expectedStateRevision ?? new OperationStateRevision(1),
            DateTimeOffset.UnixEpoch,
            idempotencyKey ?? new IdempotencyKey("promote"));

    private static T Id<T>(int value)
    {
        var guid = new Guid(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        return typeof(T) switch
        {
            var type when type == typeof(SessionEntryId) => (T) (object) new SessionEntryId(guid),
            var type when type == typeof(MessageId) => (T) (object) new MessageId(guid),
            _ => throw new InvalidOperationException($"Unsupported identifier type {typeof(T).FullName}."),
        };
    }
}
