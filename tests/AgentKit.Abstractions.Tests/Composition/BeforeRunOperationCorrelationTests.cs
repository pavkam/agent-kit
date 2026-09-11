// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using AgentKit;

/// <summary>Verifies BeforeRunOperationCorrelation behavior and contracts.</summary>
public sealed class BeforeRunOperationCorrelationTests
{
    [Fact]
    public void BeforeRunOperationCorrelation_Constructor_RoundTripsProperties()
    {
        var operationId = new OperationId(Guid.NewGuid());
        var admissionId = new AdmissionId(Guid.NewGuid());
        var correlation = new BeforeRunOperationCorrelation(operationId, admissionId);
        correlation.OperationId.ShouldBe(operationId);
        correlation.AdmissionId.ShouldBe(admissionId);
    }

    [Fact]
    public void BeforeRunOperationCorrelation_Equality_WhenSameValues_InstancesAreEqual()
    {
        var operationId = new OperationId(Guid.NewGuid());
        new BeforeRunOperationCorrelation(operationId, null).ShouldBe(new BeforeRunOperationCorrelation(operationId, null));
    }

    /// <summary>Verifies canonical scope and correlation values reject default nested identities, including record-copy mutation.</summary>
    [Fact]
    public void Constructor_WhenScopeOrCorrelationPartIsDefault_ThrowsExactArgument()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BeforeRunOperationCorrelation(default, null)).ParamName.ShouldBe("operationId");
        Should.Throw<ArgumentOutOfRangeException>(() => new BeforeRunOperationCorrelation(OperationId(), default(AdmissionId))).ParamName.ShouldBe("admissionId");
    }

    /// <summary>Verifies every hardened init accessor rejects invalid record copies without changing the original.</summary>
    [Fact]
    public void With_WhenScopeOrCorrelationPartIsDefault_ThrowsExactArgumentAndPreservesOriginal()
    {
        var before = new BeforeRunOperationCorrelation(OperationId(), new AdmissionId(Guid.Parse("66666666-6666-6666-6666-666666666666")));
        Should.Throw<ArgumentOutOfRangeException>(() => before with { OperationId = default }).ParamName.ShouldBe("operationId");
        Should.Throw<ArgumentOutOfRangeException>(() => before with { AdmissionId = default(AdmissionId) }).ParamName.ShouldBe("admissionId");
        _ = before.AdmissionId.ShouldNotBeNull();
    }

    private static OperationId OperationId() => new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
}
