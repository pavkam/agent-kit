// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

using AgentKit;

using Shouldly;

public sealed class HumanQuestionContractsTests
{
    [Fact]
    public void ThrowIfDuplicateQuestionOptionIds_WhenIdsUnique_DoesNotThrow()
    {
        ImmutableArray<HumanQuestionOption> options =
        [
            new(new QuestionOptionId("one"), "One", "First."),
            new(new QuestionOptionId("two"), "Two", "Second."),
        ];

        var action = () => ArgumentException.ThrowIfDuplicateQuestionOptionIds(options);

        action.ShouldNotThrow();
    }

    [Fact]
    public void ThrowIfDuplicateQuestionOptionIds_WhenIdsDuplicate_InfersParameterName()
    {
        var option = new HumanQuestionOption(new QuestionOptionId("same"), "One", "First.");
        ImmutableArray<HumanQuestionOption> options = [option, option];

        var action = () => ArgumentException.ThrowIfDuplicateQuestionOptionIds(options);

        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    public void HumanQuestionPrompt_WhenOptionCountOutsideBoundary_ThrowsBeforeConstruction(int count)
    {
        var options = Enumerable.Range(0, count)
            .Select(index => new HumanQuestionOption(new QuestionOptionId($"option-{index}"), "Label", "Description."))
            .ToImmutableArray();

        var action = () => Prompt(options);

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("options.Length");
    }

    [Fact]
    public void Fingerprint_WhenBehavioralInputChanges_ChangesEvidence()
    {
        var prompt = Prompt(Options());
        var original = HumanQuestionSecurityBinding.Fingerprint(
            prompt.Id, prompt.Prompt, prompt.Options, prompt.AllowsFreeText, prompt.Deadline);

        var changed = HumanQuestionSecurityBinding.Fingerprint(
            prompt.Id, prompt.Prompt, prompt.Options, true, prompt.Deadline);

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

    private static HumanQuestionPrompt Prompt(ImmutableArray<HumanQuestionOption> options) => new(
        new QuestionId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
        null,
        new ToolCallId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
        new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            null),
        AgentKit.TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human),
        "Choose.",
        options,
        false,
        DateTimeOffset.UnixEpoch.AddMinutes(1));

    private static ImmutableArray<HumanQuestionOption> Options() =>
    [
        new(new QuestionOptionId("one"), "One", "First."),
        new(new QuestionOptionId("two"), "Two", "Second."),
    ];
}
