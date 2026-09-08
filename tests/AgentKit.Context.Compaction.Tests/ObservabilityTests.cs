// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Options;

public sealed class ObservabilityTests
{
    [Fact]
    public async Task CompactAsync_WhenObserved_EmitsCorrelatedContentFreeActivity()
    {
        const string protectedContent = "never-export-compaction-source";
        var branchId = new BranchId(Guid.NewGuid());
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var coordinator = new FakeSessionCoordinator(branchId);
        coordinator.Seed([TestFactory.MessageEntry(new SessionAddress(agentId, sessionId), branchId, 1, protectedContent)]);
        var options = Options.Create(new CompactionOptions());
        var estimator = new CharacterCompactionSizeEstimator(options);
        var compactor = new DefaultCompactor(
            coordinator,
            new StructuralCompactionCutSelector(options),
            new ExtractiveCompactionStrategy(estimator, options),
            new DefaultCompactionValidator(estimator, options),
            estimator,
            new GuidIdentifierGenerator<CompactionManifestId>(static value => new CompactionManifestId(value)),
            new GuidIdentifierGenerator<SessionEntryId>(static value => new SessionEntryId(value)),
            TimeProvider.System,
            options);
        var request = TestFactory.Request(
            TestFactory.CompactionContext(agentId, sessionId),
            branchId,
            coordinator.Version,
            new SessionSequence(1),
            minimumRetainedEntries: 5);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.ContextCompact
                && Equals(
                    activity.GetTagItem(AgentKitTagNames.CompactionId),
                    request.Context.CompactionId.ToString()));

        _ = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.CompactionId).ShouldBe(request.Context.CompactionId.ToString());
        activity.Tags.Values.Select(static value => value?.ToString()).ShouldNotContain(protectedContent);
    }
}
