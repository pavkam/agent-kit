// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionRunBusy behavior and contracts.</summary>
public sealed class SessionRunBusyTests
{
    private static readonly Guid _operationGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid _runGuid = Guid.Parse("55555555-5555-5555-5555-555555555555");
    [Fact]
    public void SessionRunBusy_Equality_WhenSameValues_InstancesAreEqual() => new SessionRunBusy(new OperationId(_operationGuid), new RunId(_runGuid)).ShouldBe(new SessionRunBusy(new OperationId(_operationGuid), new RunId(_runGuid)));
    [Fact]
    public void LeaseOutcomeConstructors_WhenArgumentsAreInvalid_ThrowExactExceptions()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SessionRunBusy(default, new RunId(Guid.NewGuid()))).ParamName.ShouldBe("activeOperationId");
        Should.Throw<ArgumentOutOfRangeException>(() => new SessionRunBusy(new OperationId(Guid.NewGuid()), default)).ParamName.ShouldBe("activeRunId");
    }
}
