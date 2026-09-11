// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies ToolCallOutcome behavior and contracts.</summary>
public sealed class ToolCallOutcomeTests
{
    [Fact]
    public void ToolCallOutcome_Equality_WhenSameValues_InstancesAreEqual() => new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty).ShouldBe(new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty));
    [Fact]
    public void ToolCallOutcome_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolCallOutcome(ToolCallOutcomeKind.Success, null, null!));
        exception.ParamName.ShouldBe("extensions");
    }
}
