// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

using AgentKit;

/// <summary>Verifies ContextTrust behavior and contracts.</summary>
public sealed class ContextTrustTests
{
    [Fact]
    public void ContextEnums_WhenEnumerated_ContainOnlyNormativeValues() => Enum.GetValues<ContextTrust>().ShouldBe([ContextTrust.Framework, ContextTrust.HostPolicy, ContextTrust.AgentDefinition, ContextTrust.Workspace, ContextTrust.User, ContextTrust.RetrievedData, ContextTrust.ToolData, ContextTrust.ModelGenerated, ContextTrust.Package,]);
}
