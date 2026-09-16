// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Verifies StoredAdmission behavior and contracts.</summary>
public sealed class StoredAdmissionTests
{
    [Fact]
    public void StoredAdmission_WhenConstructed_RetainsEntryIdAndReceiptAndAllowsInputReplacement()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var laneId = new ExecutionLaneId(Guid.NewGuid());
        var identity = TestFactory.Identity();
        var input = new AgentInput(new InputId(Guid.NewGuid()), InputDelivery.FollowUp,
            [new TextPart("hello", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var preprocessing = new InputPreprocessingManifest(
            new ConfigurationVersion(1), new InputFingerprint("sha256:original"), new InputFingerprint("sha256:effective"));
        var admitted = new AdmittedInput(new AdmissionId(Guid.NewGuid()), address.AgentId, address.SessionId, laneId,
            identity, new SessionSequence(1), input, input, preprocessing, DateTimeOffset.UnixEpoch);
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null);
        var entryId = new SessionEntryId(Guid.NewGuid());
        var receipt = new AdmissionReceipt(admitted.AdmissionId, admitted.OriginalPayload.Id, admitted.AgentId,
            admitted.SessionId, admitted.ExecutionLaneId, admitted.AdmittedSequence, existing: false);

        var stored = new StoredAdmission(admitted, correlation, entryId, receipt);
        var promoted = new AdmittedInput(admitted.AdmissionId, admitted.AgentId, admitted.SessionId, admitted.ExecutionLaneId,
            admitted.Identity, admitted.AdmittedSequence, admitted.OriginalPayload, admitted.EffectivePayload,
            admitted.Preprocessing, admitted.AdmittedAt, new SessionSequence(2));
        stored.Input = promoted;

        stored.EntryId.ShouldBe(entryId);
        stored.Correlation.ShouldBe(correlation);
        stored.Receipt.ShouldBe(receipt);
        stored.Input.ShouldBe(promoted);
    }
}
