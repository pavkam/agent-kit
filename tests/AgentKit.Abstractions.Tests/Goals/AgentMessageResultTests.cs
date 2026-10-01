// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies the agent-message outcome types.</summary>
public sealed class AgentMessageResultTests
{
    [Fact]
    public void Accepted_WhenReceiptIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentMessageAccepted(null!)).ParamName.ShouldBe("receipt");

    [Fact]
    public void Accepted_WhenReceiptIsGiven_RetainsIt()
    {
        var receipt = new AdmissionReceipt(
            new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), GoalTestData.NewAgent(), GoalTestData.NewSession(),
            new ExecutionLaneId(Guid.NewGuid()), new SessionSequence(1), existing: false);

        new AgentMessageAccepted(receipt).Receipt.ShouldBeSameAs(receipt);
    }

    [Fact]
    public void Rejected_WhenValid_RetainsKindAndReason()
    {
        var rejected = new AgentMessageRejected(AgentMessageRejectionKind.Conflict, "reused");

        rejected.Kind.ShouldBe(AgentMessageRejectionKind.Conflict);
        rejected.SafeReason.ShouldBe("reused");
    }

    [Fact]
    public void Rejected_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentMessageRejected((AgentMessageRejectionKind) 99, "r")).ParamName.ShouldBe("kind");

    [Fact]
    public void Rejected_WhenReasonIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentMessageRejected(AgentMessageRejectionKind.Rejected, " ")).ParamName.ShouldBe("safeReason");
}
