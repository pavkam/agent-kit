// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies the journaled approval-wait manifest's argument constraints and value semantics.</summary>
public sealed class DurableApprovalWaitManifestTests
{
    private static readonly Guid SecurityRequestId = Guid.Parse("25000000-0000-0000-0000-000000000001");
    private static readonly Guid ApprovalRequestId = Guid.Parse("26000000-0000-0000-0000-000000000001");

    [Fact]
    public void Constructor_WhenSecurityRequestIdIsEmpty_ThrowsForThatArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableApprovalWaitManifest(Guid.Empty, ApprovalRequestId, "ProcessExecute"))
            .ParamName.ShouldBe("securityRequestId");

    [Fact]
    public void Constructor_WhenApprovalRequestIdIsEmpty_ThrowsForThatArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new DurableApprovalWaitManifest(SecurityRequestId, Guid.Empty, "ProcessExecute"))
            .ParamName.ShouldBe("approvalRequestId");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t")]
    public void Constructor_WhenOperationKindIsBlank_ThrowsForThatArgument(string? operationKind) =>
        Should.Throw<ArgumentException>(
                () => new DurableApprovalWaitManifest(SecurityRequestId, ApprovalRequestId, operationKind!))
            .ParamName.ShouldBe("operationKind");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsEveryProperty()
    {
        var manifest = new DurableApprovalWaitManifest(SecurityRequestId, ApprovalRequestId, "ProcessExecute");

        manifest.SecurityRequestId.ShouldBe(SecurityRequestId);
        manifest.ApprovalRequestId.ShouldBe(ApprovalRequestId);
        manifest.OperationKind.ShouldBe("ProcessExecute");
    }

    [Fact]
    public void Equals_WhenEveryFieldAgrees_IsStructural() =>
        new DurableApprovalWaitManifest(SecurityRequestId, ApprovalRequestId, "ProcessExecute")
            .ShouldBe(new DurableApprovalWaitManifest(SecurityRequestId, ApprovalRequestId, "ProcessExecute"));
}
