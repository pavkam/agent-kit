// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

/// <summary>
/// A synthetic hook event-argument type used only by this test project to
/// exercise <see cref="DefaultHookDispatcher"/> ordering, mutation
/// validation, and short-circuiting without depending on any real hook
/// point defined by a product package.
/// </summary>
internal sealed class TestHookEventArgs: AgentHookEventArgs, IShortCircuitingHookArgs
{
    public TestHookEventArgs()
        : base(
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null),
            DateTimeOffset.UnixEpoch,
            new HookInvocationId(Guid.NewGuid()))
    {
    }

    public List<HookId> InvocationOrder { get; } = [];

    public string? Payload { get; set; }

    public bool RejectPayload { get; set; }

    public bool IsShortCircuited { get; set; }

    public override void Validate()
    {
        if (RejectPayload)
        {
            throw new HookValidationException("Payload was rejected by test policy.");
        }
    }
}
