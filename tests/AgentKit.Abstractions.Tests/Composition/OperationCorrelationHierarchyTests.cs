// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using AgentKit;

public sealed class OperationCorrelationHierarchyTests
{
    [Fact]
    public void InRunOperationCorrelation_Equality_WhenSameValues_InstancesAreEqual()
    {
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());

        new InRunOperationCorrelation(operationId, runId, null).ShouldBe(
            new InRunOperationCorrelation(operationId, runId, null));
    }

    [Fact]
    public void InRunOperationCorrelation_Constructor_RoundTripsProperties()
    {
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());

        var correlation = new InRunOperationCorrelation(operationId, runId, turnId);

        correlation.OperationId.ShouldBe(operationId);
        correlation.RunId.ShouldBe(runId);
        correlation.TurnId.ShouldBe(turnId);
    }

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

        new BeforeRunOperationCorrelation(operationId, null).ShouldBe(
            new BeforeRunOperationCorrelation(operationId, null));
    }

    [Fact]
    public void AfterRunOperationCorrelation_Constructor_RoundTripsProperties()
    {
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());

        var correlation = new AfterRunOperationCorrelation(operationId, runId);

        correlation.OperationId.ShouldBe(operationId);
        correlation.CausalRunId.ShouldBe(runId);
    }

    [Fact]
    public void AfterRunOperationCorrelation_Equality_WhenSameValues_InstancesAreEqual()
    {
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());

        new AfterRunOperationCorrelation(operationId, runId).ShouldBe(
            new AfterRunOperationCorrelation(operationId, runId));
    }

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

    [Fact]
    public void AgentCapabilityReference_Constructor_RoundTripsProperties()
    {
        var capabilityId = new CapabilityId("cap");
        var profileId = new CapabilityProfileId("profile");

        var reference = new AgentCapabilityReference(capabilityId, profileId, required: true);

        reference.CapabilityId.ShouldBe(capabilityId);
        reference.ProfileId.ShouldBe(profileId);
        reference.Required.ShouldBeTrue();
    }

    [Fact]
    public void AgentCapabilityReference_Equality_WhenSameValues_InstancesAreEqual()
    {
        var capabilityId = new CapabilityId("cap");
        var profileId = new CapabilityProfileId("profile");

        new AgentCapabilityReference(capabilityId, profileId, true).ShouldBe(
            new AgentCapabilityReference(capabilityId, profileId, true));
    }

    [Fact]
    public void AgentCapabilityReference_Equality_WhenDifferentRequired_InstancesAreNotEqual()
    {
        var capabilityId = new CapabilityId("cap");
        var profileId = new CapabilityProfileId("profile");

        new AgentCapabilityReference(capabilityId, profileId, true).ShouldNotBe(
            new AgentCapabilityReference(capabilityId, profileId, false));
    }
}
