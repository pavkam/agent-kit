// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

using AgentKit;

using Shouldly;

/// <summary>Verifies HumanQuestionSecurityBinding behavior and contracts.</summary>
public sealed class HumanQuestionSecurityBindingTests
{
    [Fact]
    public void Fingerprint_WhenBehavioralInputChanges_ChangesEvidence()
    {
        var prompt = Prompt(Options());
        var original = HumanQuestionSecurityBinding.Fingerprint(prompt.Id, prompt.Prompt, prompt.Options, prompt.AllowsFreeText, prompt.Deadline);
        var changed = HumanQuestionSecurityBinding.Fingerprint(prompt.Id, prompt.Prompt, prompt.Options, true, prompt.Deadline);
        changed.ShouldNotBe(original);
    }

    [Fact]
    public void Resource_WhenQuestionKnown_UsesCanonicalApplicationStateIdentity()
    {
        var id = new QuestionId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var resource = HumanQuestionSecurityBinding.Resource(id);
        resource.Kind.ShouldBe(ProtectedResourceKind.ApplicationState);
        resource.Identifier.ShouldBe($"human-question:{id}");
    }

    private static HumanQuestionPrompt Prompt(ImmutableArray<HumanQuestionOption> options) => new(new QuestionId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000002")), null, new ToolCallId(Guid.Parse("30000000-0000-0000-0000-000000000003")), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")), null), TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human), "Choose.", options, false, DateTimeOffset.UnixEpoch.AddMinutes(1));
    private static ImmutableArray<HumanQuestionOption> Options() => [new(new QuestionOptionId("one"), "One", "First."), new(new QuestionOptionId("two"), "Two", "Second."),];
}
