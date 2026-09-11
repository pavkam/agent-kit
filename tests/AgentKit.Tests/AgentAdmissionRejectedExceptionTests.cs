// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;
/// <summary>Verifies AgentAdmissionRejectedException behavior and contracts.</summary>
[Collection(AdmissionObservabilityGroup.Name)]
public sealed class AgentAdmissionRejectedExceptionTests
{
    [Fact]
    public void AgentAdmissionRejectedException_WhenRejectionIsNull_ThrowsBeforeConstruction()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AgentAdmissionRejectedException(null!));
        exception.ParamName.ShouldBe("rejection");
    }

    [Fact]
    public void AgentAdmissionRejectedException_WhenRejectionIsValid_PreservesItsEvidence()
    {
        var rejection = new AgentAdmissionRejection(CompositionTestData.AgentId, new AgentDefinitionRevision(1), new AgentCatalogVersion(2), "removed");
        var exception = new AgentAdmissionRejectedException(rejection);
        exception.Rejection.ShouldBeSameAs(rejection);
        exception.Message.ShouldBe("removed");
    }
}
