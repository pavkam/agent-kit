// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Question.Tests;

internal sealed class RecordingQuestionBroker: IHumanQuestionBroker
{
    internal List<HumanQuestionRequest> Requests { get; } = [];

    internal Func<HumanQuestionRequest, HumanQuestionResult> Result { get; set; } = static request =>
        new HumanQuestionAnswered(
            request.Id,
            new HumanQuestionAnswer(
                request.Options[0].Id,
                null,
                TestData.Identity,
                DateTimeOffset.UnixEpoch.AddMinutes(1)));

    internal bool GrantMatched { get; private set; }

    public ComponentId SecurityAudience { get; } = new("test.question.broker");

    public ValueTask<HumanQuestionResult> AskAsync(
        HumanQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        GrantMatched = request.Grant.Audience == SecurityAudience
            && request.Grant.Kind == SecurityOperationKind.StateMutation
            && request.Grant.Effect == SecurityEffect.Create
            && request.Grant.Resources.SequenceEqual([HumanQuestionSecurityBinding.Resource(request.Id)])
            && request.Grant.InputFingerprint == HumanQuestionSecurityBinding.Fingerprint(
                request.Id,
                request.Prompt,
                request.Options,
                request.AllowsFreeText,
                request.Deadline);
        return ValueTask.FromResult(GrantMatched
            ? Result(request)
            : new HumanQuestionUnavailable(request.Id, "Grant mismatch."));
    }
}

internal sealed class RecordingSecurityAuthority(bool allow = true): ISecurityAuthority
{
    internal List<SecurityRequest> Requests { get; } = [];

    public ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return !allow
            ? ValueTask.FromResult<SecurityDecision>(new SecurityDenied(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityDenial("test.denied", "Denied.")))
            : ValueTask.FromResult<SecurityDecision>(new SecurityAllowed(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityGrant(
                    new GrantId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
                    request.Id,
                    request.Scope,
                    request.Identity,
                    request.Audience,
                    request.Kind,
                    request.Effect,
                    request.Resources,
                    request.InputFingerprint,
                    new SecurityPolicyVersion(1),
                    new SecurityRevocationVersion(1),
                    DateTimeOffset.UnixEpoch,
                    request.Deadline,
                    1)));
    }
}

internal sealed class FixedSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    public SecurityRequestId Create() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
}

internal sealed class FixedQuestionIdGenerator: IIdentifierGenerator<QuestionId>
{
    internal int Calls { get; private set; }

    public QuestionId Create()
    {
        Calls++;
        return TestData.QuestionId;
    }
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}

internal static class TestData
{
    internal static QuestionId QuestionId { get; } = new(Guid.Parse("30000000-0000-0000-0000-000000000003"));

    internal static ExecutionIdentity Identity { get; } = AgentKit.TestSupport.TestExecutionIdentity.Create(
        new TenantId("tenant"),
        new PrincipalId("principal"),
        ExecutionSubjectKind.Human);
}
