// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Question.Tests;



/// <summary>Verifies HumanQuestionRequest behavior and contracts.</summary>
public sealed class HumanQuestionRequestTests
{
    [Fact]
    public void HumanQuestionRequest_WhenOptionIdsDuplicate_ThrowsExactArgumentException()
    {
        var option = new HumanQuestionOption(new QuestionOptionId("same"), "One", "First.");
        var action = () => new HumanQuestionRequest(TestData.QuestionId, new AgentId(Guid.NewGuid()), null, new ToolCallId(Guid.NewGuid()), new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), TestData.Identity, "Choose.", [option, option], false, DateTimeOffset.UnixEpoch.AddMinutes(1), Grant());
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("options");
    }

    private static SecurityGrant Grant() => new(new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), new SecurityAuthorizationScope(new AgentId(Guid.NewGuid()), null, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null)), TestData.Identity, new ComponentId("test"), SecurityOperationKind.StateMutation, SecurityEffect.Create, [HumanQuestionSecurityBinding.Resource(TestData.QuestionId)], new InputFingerprint("fingerprint"), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), 1);
}
