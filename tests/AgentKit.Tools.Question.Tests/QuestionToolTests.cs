// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Question.Tests;



/// <summary>Verifies QuestionTool behavior and contracts.</summary>
public sealed class QuestionToolTests
{
    private const string ValidArguments = /*lang=json,strict*/ """
        {
          "question": "Choose a deployment strategy.",
          "options": [
            { "id": "safe", "label": "Safe", "description": "Roll out gradually." },
            { "id": "fast", "label": "Fast", "description": "Deploy immediately." }
          ]
        }
        """;
    [Theory]
    [InlineData( /*lang=json,strict*/"{}")]
    [InlineData( /*lang=json,strict*/"{\"question\":\"Q\",\"options\":[]}")]
    [InlineData( /*lang=json,strict*/"{\"question\":\"Q\",\"options\":[{\"id\":\"x\",\"label\":\"X\",\"description\":\"D\"},{\"id\":\"x\",\"label\":\"Y\",\"description\":\"E\"}]}")]
    [InlineData( /*lang=json,strict*/"{\"question\":\"Q\",\"options\":[{\"id\":\"x\",\"label\":\"X\",\"description\":\"D\"},{\"id\":\"y\",\"label\":\"Y\",\"description\":\"E\"}],\"extra\":true}")]
    [InlineData( /*lang=json,strict*/"{\"question\":\"Q\",\"options\":[{\"id\":\"x\",\"label\":\"X\",\"description\":\"D\"},{\"id\":\"y\",\"label\":\"Y\",\"description\":\"E\"}],\"timeout_seconds\":0}")]
    [InlineData( /*lang=json,strict*/"{\"question\":\"Q\",\"options\":[{\"id\":123,\"label\":\"X\",\"description\":\"D\"},{\"id\":\"y\",\"label\":\"Y\",\"description\":\"E\"}]}")]
    [InlineData( /*lang=json,strict*/"{\"question\":\"Q\",\"options\":[{\"id\":\"x\",\"label\":\"X\",\"description\":\"D\"},{\"id\":\"y\",\"label\":\"Y\",\"description\":\"E\"}],\"allow_free_text\":\"yes\"}")]
    public async Task InvokeAsync_WhenArgumentsInvalid_PerformsNoIdentityAllocationAuthorizationOrPublication(string json)
    {
        var broker = new RecordingQuestionBroker();
        var authority = new RecordingSecurityAuthority();
        var ids = new FixedQuestionIdGenerator();
        var result = await Tool(broker, authority, ids).InvokeAsync(Request(json), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        ids.Calls.ShouldBe(0);
        authority.Requests.ShouldBeEmpty();
        broker.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Descriptor_WhenRead_ExposesStableIdentity()
    {
        var tool = Tool(new RecordingQuestionBroker(), new RecordingSecurityAuthority());

        tool.Descriptor.Id.ShouldBe(QuestionTool.Id);
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorized_BindsAndReenforcesExactQuestionEvidence()
    {
        var broker = new RecordingQuestionBroker();
        var authority = new RecordingSecurityAuthority();
        var result = await Tool(broker, authority).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var security = authority.Requests.ShouldHaveSingleItem();
        security.Audience.ShouldBe(broker.SecurityAudience);
        security.Kind.ShouldBe(SecurityOperationKind.StateMutation);
        security.Effect.ShouldBe(SecurityEffect.Create);
        security.Resources.ShouldBe([HumanQuestionSecurityBinding.Resource(TestData.QuestionId)]);
        var published = broker.Requests.ShouldHaveSingleItem();
        published.Grant.RequestId.ShouldBe(security.Id);
        broker.GrantMatched.ShouldBeTrue();
        published.Deadline.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(5));
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorityDenies_ReturnsRejectedWithoutPublication()
    {
        var broker = new RecordingQuestionBroker();
        var result = await Tool(broker, new RecordingSecurityAuthority(false)).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.FailureReason.ShouldBe("Denied.");
        broker.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenAnswered_ProjectsChoiceAsNonAuthoritativeData()
    {
        var result = await Tool(new RecordingQuestionBroker(), new RecordingSecurityAuthority()).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);
        using var json = Json(result);
        json.RootElement.GetProperty("question_id").GetString().ShouldBe(TestData.QuestionId.ToString());
        json.RootElement.GetProperty("selected_option_id").GetString().ShouldBe("safe");
        json.RootElement.GetProperty("selected_option_label").GetString().ShouldBe("Safe");
        json.RootElement.GetProperty("instruction_authority").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenFreeTextAllowed_ProjectsBoundedSupplementaryText()
    {
        var broker = new RecordingQuestionBroker
        {
            Result = static request => new HumanQuestionAnswered(request.Id, new HumanQuestionAnswer(request.Options[1].Id, "because speed matters", TestData.Identity, request.Deadline)),
        };
        var arguments = ValidArguments.TrimEnd('}', '\r', '\n', ' ') + ",\"allow_free_text\":true}";
        var result = await Tool(broker, new RecordingSecurityAuthority()).InvokeAsync(Request(arguments), TestContext.Current.CancellationToken);
        using var json = Json(result);
        json.RootElement.GetProperty("free_text").GetString().ShouldBe("because speed matters");
    }

    [Fact]
    public async Task InvokeAsync_WhenBrokerReturnsDisallowedFreeText_RejectsBrokerResult()
    {
        var broker = new RecordingQuestionBroker
        {
            Result = static request => new HumanQuestionAnswered(request.Id, new HumanQuestionAnswer(request.Options[0].Id, "injected", TestData.Identity, request.Deadline)),
        };
        var result = await Tool(broker, new RecordingSecurityAuthority()).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason!.ShouldContain("authorized response shape");
        result.Content.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenBrokerReturnsUnknownOption_RejectsBrokerResult()
    {
        var broker = new RecordingQuestionBroker
        {
            Result = static request => new HumanQuestionAnswered(request.Id, new HumanQuestionAnswer(new QuestionOptionId("unknown"), null, TestData.Identity, request.Deadline)),
        };
        var result = await Tool(broker, new RecordingSecurityAuthority()).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Content.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenBrokerReturnsDifferentQuestion_RejectsBrokerResult()
    {
        var broker = new RecordingQuestionBroker
        {
            Result = static request => new HumanQuestionTimedOut(new QuestionId(Guid.Parse("90000000-0000-0000-0000-000000000009"))),
        };
        var result = await Tool(broker, new RecordingSecurityAuthority()).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);
        result.Outcome.FailureReason!.ShouldContain("different question");
    }

    [Fact]
    public async Task InvokeAsync_WhenQuestionTimesOut_ReturnsTerminalFailure()
    {
        var broker = new RecordingQuestionBroker
        {
            Result = static request => new HumanQuestionTimedOut(request.Id),
        };
        var result = await Tool(broker, new RecordingSecurityAuthority()).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason!.ShouldContain("deadline");
    }

    [Fact]
    public async Task InvokeAsync_WhenChannelUnavailable_PreservesSafeFailure()
    {
        var broker = new RecordingQuestionBroker
        {
            Result = static request => new HumanQuestionUnavailable(request.Id, "No interactive channel is attached."),
        };
        var result = await Tool(broker, new RecordingSecurityAuthority()).InvokeAsync(Request(ValidArguments), TestContext.Current.CancellationToken);
        result.Outcome.FailureReason.ShouldBe("No interactive channel is attached.");
    }

    [Fact]
    public async Task InvokeAsync_WhenTimeoutRequested_UsesRequestedBoundAndOneMinuteGrantDeadline()
    {
        var broker = new RecordingQuestionBroker();
        var authority = new RecordingSecurityAuthority();
        var arguments = ValidArguments.TrimEnd('}', '\r', '\n', ' ') + ",\"timeout_seconds\":600}";
        _ = await Tool(broker, authority).InvokeAsync(Request(arguments), TestContext.Current.CancellationToken);
        broker.Requests.ShouldHaveSingleItem().Deadline.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(10));
        authority.Requests.ShouldHaveSingleItem().Deadline.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(1));
    }

    private static QuestionTool Tool(IHumanQuestionBroker broker, ISecurityAuthority authority, FixedQuestionIdGenerator? questionIds = null, QuestionToolOptions? options = null) => new(broker, authority, new FixedSecurityRequestIdGenerator(), questionIds ?? new FixedQuestionIdGenerator(), new FixedTimeProvider(), Options.Create(options ?? new QuestionToolOptions()));
    private static JsonDocument Json(ToolInvocationResult result) => JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
    private static ToolInvocationRequest Request(string json) => new(TestSupport.TestSecurityEvidence.ToolContext(new AgentId(Guid.Parse("40000000-0000-0000-0000-000000000004")), new SessionId(Guid.Parse("50000000-0000-0000-0000-000000000005")), new ToolCallId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new InRunOperationCorrelation(new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")), new RunId(Guid.Parse("80000000-0000-0000-0000-000000000008")), null), TestData.Identity), JsonDocument.Parse(json).RootElement, DateTimeOffset.UnixEpoch);
}
