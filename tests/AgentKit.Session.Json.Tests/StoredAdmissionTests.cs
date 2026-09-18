// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies the mutable in-memory admission projection retained under the store gate.</summary>
public sealed class StoredAdmissionTests
{
    /// <summary>Verifies every constructor argument is retained unchanged.</summary>
    [Fact]
    public void Constructor_WhenCalled_RetainsEveryField()
    {
        var input = Input(promoted: false);
        var correlation = new BeforeRunOperationCorrelation(JsonSessionStoreTests.Identifier<OperationId>(300), null);
        var entryId = JsonSessionStoreTests.Identifier<SessionEntryId>(301);
        var receipt = Receipt(input);

        var stored = new StoredAdmission(input, correlation, entryId, receipt);

        stored.Input.ShouldBeSameAs(input);
        stored.Correlation.ShouldBe(correlation);
        stored.EntryId.ShouldBe(entryId);
        stored.Receipt.ShouldBeSameAs(receipt);
    }

    /// <summary>Verifies the input snapshot can be replaced as promotion advances it, since only <see cref="StoredAdmission.Input"/> is mutable.</summary>
    [Fact]
    public void Input_WhenReplacedAfterPromotion_ReflectsTheNewSnapshot()
    {
        var original = Input(promoted: false);
        var correlation = new BeforeRunOperationCorrelation(JsonSessionStoreTests.Identifier<OperationId>(302), null);
        var entryId = JsonSessionStoreTests.Identifier<SessionEntryId>(303);
        var stored = new StoredAdmission(original, correlation, entryId, Receipt(original));

        var promoted = Input(promoted: true);
        stored.Input = promoted;

        stored.Input.ShouldBeSameAs(promoted);
        _ = stored.Input.PromotedSequence.ShouldNotBeNull();
    }

    private static AdmittedInput Input(bool promoted)
    {
        var original = new AgentInput(
            JsonSessionStoreTests.Identifier<InputId>(304), InputDelivery.FollowUp,
            [new TextPart("original", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var effective = new AgentInput(
            original.Id, InputDelivery.FollowUp,
            [new TextPart("effective", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var preprocessing = new InputPreprocessingManifest(
            new ConfigurationVersion(1), new InputFingerprint("sha256:original"), new InputFingerprint("sha256:effective"));
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant-admission"), new PrincipalId("owner"), ExecutionSubjectKind.Human);
        return new AdmittedInput(
            JsonSessionStoreTests.Identifier<AdmissionId>(305), JsonSessionStoreTests.Identifier<AgentId>(306),
            JsonSessionStoreTests.Identifier<SessionId>(307), JsonSessionStoreTests.Identifier<ExecutionLaneId>(308),
            identity, new SessionSequence(1), original, effective, preprocessing, DateTimeOffset.UnixEpoch,
            promoted ? new SessionSequence(2) : null);
    }

    private static AdmissionReceipt Receipt(AdmittedInput input) => new(
        input.AdmissionId, input.OriginalPayload.Id, input.AgentId, input.SessionId, input.ExecutionLaneId,
        input.AdmittedSequence, existing: false);
}
