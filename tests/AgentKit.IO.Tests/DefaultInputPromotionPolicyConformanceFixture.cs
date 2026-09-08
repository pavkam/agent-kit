// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using AgentKit.Conformance;

/// <summary>Composes the first-party promotion policy through its public registration for portable conformance tests.</summary>
public sealed class DefaultInputPromotionPolicyConformanceFixture: IInputPromotionPolicyConformanceFixture
{
    private readonly ServiceProvider _provider = new ServiceCollection().AddInputPromotionPolicy().BuildServiceProvider();

    /// <inheritdoc/>
    public IInputPromotionPolicy Policy => _provider.GetRequiredService<IInputPromotionPolicy>();

    /// <inheritdoc/>
    public InputPromotionContext CreateSteeringContext() => Context(4, Admitted(3), Admitted(1, InputDelivery.FollowUp), Admitted(2));

    /// <inheritdoc/>
    public ImmutableArray<AdmissionId> ExpectedSteeringAdmissions() => [Admission(2), Admission(3)];

    /// <inheritdoc/>
    public InputPromotionContext CreateOverLimitContext() => Context(1, Admitted(1), Admitted(2));

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();

    private static InputPromotionContext Context(int maximumPromotions, params AdmittedInput[] inputs) =>
        new(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(2),
            new SessionBranchCursor(Branch(), Entry(9)), new SessionSequence(10), new SessionVersion(4), null,
            PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), [.. inputs], maximumPromotions);

    private static AdmittedInput Admitted(long sequence, InputDelivery delivery = InputDelivery.Steer)
    {
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var input = new AgentInput(Input(sequence), delivery, [new TextPart("input", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        return new AdmittedInput(Admission(sequence), Agent(), Session(), Lane(), identity,
            new SessionSequence(sequence), input, input,
            new InputPreprocessingManifest(new ConfigurationVersion(1), new InputFingerprint($"o:{sequence}"), new InputFingerprint($"e:{sequence}")),
            DateTimeOffset.UnixEpoch);
    }

    private static AdmissionId Admission(long value) => new(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    private static InputId Input(long value) => new(Guid.Parse($"10000000-0000-0000-0000-{value:000000000000}"));
    private static SessionEntryId Entry(long value) => new(Guid.Parse($"20000000-0000-0000-0000-{value:000000000000}"));
    private static AgentId Agent() => new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static SessionId Session() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static ExecutionLaneId Lane() => new(Guid.Parse("45000000-0000-0000-0000-000000000001"));
    private static BranchId Branch() => new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    private static TurnId Turn() => new(Guid.Parse("60000000-0000-0000-0000-000000000001"));
    private static TurnId NextTurn() => new(Guid.Parse("60000000-0000-0000-0000-000000000002"));
    private static InRunOperationCorrelation Operation() => new(
        new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000001")),
        new RunId(Guid.Parse("80000000-0000-0000-0000-000000000001")), Turn());
}
