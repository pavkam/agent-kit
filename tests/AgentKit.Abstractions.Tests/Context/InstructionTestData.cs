// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Valid instruction samples for abstraction tests.</summary>
internal static class InstructionTestData
{
    internal static SystemMessage SystemMessage(string text) => new(
        new MessageId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
        new SessionId(Guid.Parse("30000000-0000-0000-0000-000000000001")),
        null,
        new BranchId(Guid.Parse("40000000-0000-0000-0000-000000000001")),
        null,
        null,
        DateTimeOffset.UnixEpoch,
        MessageState.Complete,
        [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
        ExtensionData.Empty);
}
