// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using AgentKit;

/// <summary>Verifies OperationCorrelation behavior and contracts.</summary>
public sealed class OperationCorrelationTests
{
    [Fact]
    public void OperationCorrelation_Hierarchy_EveryLeafDerivesFromOperationCorrelation()
    {
        OperationCorrelation inRun = new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);
        OperationCorrelation beforeRun = new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null);
        OperationCorrelation afterRun = new AfterRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()));
        _ = inRun.ShouldBeOfType<InRunOperationCorrelation>();
        _ = beforeRun.ShouldBeOfType<BeforeRunOperationCorrelation>();
        _ = afterRun.ShouldBeOfType<AfterRunOperationCorrelation>();
    }
}
