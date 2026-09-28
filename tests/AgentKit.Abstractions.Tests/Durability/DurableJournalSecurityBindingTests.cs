// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableJournalSecurityBinding"/> behavior and contracts.</summary>
public sealed class DurableJournalSecurityBindingTests
{
    [Fact]
    public void Resource_WhenJournalKeyIsDefault_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            DurableJournalSecurityBinding.Resource(default, DurabilityTestData.Address()));

        exception.ParamName.ShouldBe("journalKey");
    }

    [Fact]
    public void Resource_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            DurableJournalSecurityBinding.Resource(new DurableJournalKey("journal"), null!));

        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void Resource_WhenArgumentsAreValid_BindsEveryAddressComponent()
    {
        var address = DurabilityTestData.Address();

        var resource = DurableJournalSecurityBinding.Resource(new DurableJournalKey("journal"), address);

        resource.Kind.ShouldBe(ProtectedResourceKind.ApplicationState);
        resource.Identifier.ShouldStartWith("durable-journal:journal:");
        resource.Identifier.ShouldContain(address.AgentId.ToString());
        resource.Identifier.ShouldContain(address.SessionId.ToString());
        resource.Identifier.ShouldContain(address.RunId.ToString());
        resource.Identifier.ShouldContain(address.OperationId.ToString());
    }

    [Fact]
    public void Resource_WhenAddressesDiffer_ProducesDistinctResources()
    {
        var journalKey = new DurableJournalKey("journal");
        var first = DurableJournalSecurityBinding.Resource(journalKey, DurabilityTestData.Address());
        var other = DurableJournalSecurityBinding.Resource(
            journalKey,
            DurabilityTestData.Address() with { OperationId = new OperationId(Guid.Parse("0a000000-0000-0000-0000-000000000099")) });

        other.ShouldNotBe(first);
    }

    [Fact]
    public void Fingerprint_WhenSameStartIsSupplied_IsStable()
    {
        var first = DurableJournalSecurityBinding.Fingerprint(Start());
        var second = DurableJournalSecurityBinding.Fingerprint(Start());

        second.ShouldBe(first);
    }

    [Fact]
    public void Fingerprint_WhenRequestShapesDiffer_ProducesDistinctDigests()
    {
        var startDigest = DurableJournalSecurityBinding.Fingerprint(Start());
        var addressDigest = DurableJournalSecurityBinding.Fingerprint(DurabilityTestData.Address());

        addressDigest.ShouldNotBe(startDigest);
    }

    [Fact]
    public void Fingerprint_WhenCheckpointTerminalAndWaitingDiffer_ProducesDistinctDigests()
    {
        var checkpoint = DurableJournalSecurityBinding.Fingerprint(DurabilityTestData.Checkpoint());
        var terminal = DurableJournalSecurityBinding.Fingerprint(DurabilityTestData.Result());
        var waiting = DurableJournalSecurityBinding.Fingerprint(new DurableOperationWaiting(
            DurabilityTestData.Binding(),
            DurabilityTestData.Token,
            DurabilityTestData.Now,
            SideEffectCertainty.Unknown,
            notBefore: DurabilityTestData.Now.AddMinutes(1)));

        new[] { checkpoint, terminal, waiting }.Distinct().Count().ShouldBe(3);
    }

    private static DurableOperationStart Start() =>
        new(
            DurabilityTestData.Descriptor(),
            DurabilityTestData.Payload(),
            DurabilityTestData.Token,
            DurabilityTestData.Now);
}
