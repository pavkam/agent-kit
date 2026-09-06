// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

using Microsoft.Extensions.Options;

public sealed class DefaultCompactionValidatorTests
{
    private readonly AgentId _agentId = new(Guid.NewGuid());
    private readonly SessionId _sessionId = new(Guid.NewGuid());
    private readonly BranchId _branchId = new(Guid.NewGuid());

    [Fact]
    public void Constructor_WhenEstimatorNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DefaultCompactionValidator(null!, Options.Create(new CompactionOptions())));

        exception.ParamName.ShouldBe("estimator");
    }

    [Fact]
    public void Constructor_WhenOptionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DefaultCompactionValidator(CreateEstimator(), null!));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task ValidateAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var validator = CreateValidator();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => validator.ValidateAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task ValidateAsync_WhenCandidateValid_ReturnsValidated()
    {
        var validator = CreateValidator();
        var address = Address();
        var entry = TestFactory.MessageEntry(address, _branchId, 1, new string('x', 1000));
        var source = Source([entry]);
        var cut = new CompactionCut(
            new CompactionSourceRange(entry.Sequence, entry.Sequence), new SessionSequence(2), [entry.Id]);
        var candidate = Candidate("short summary", entry.Sequence, new SessionSequence(2));

        var result = await validator.ValidateAsync(
            new CompactionValidationRequest(
                TestFactory.Request(source.Context, _branchId, new SessionVersion(1), entry.Sequence, minimumReductionRatio: 0.1),
                source,
                cut,
                candidate),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<CompactionValidated>();
    }

    [Fact]
    public async Task ValidateAsync_WhenCheckpointTextBlank_ReturnsInvalidStructureIssue()
    {
        var validator = CreateValidator();
        var address = Address();
        var entry = TestFactory.MessageEntry(address, _branchId, 1, "some content");
        var source = Source([entry]);
        var cut = new CompactionCut(
            new CompactionSourceRange(entry.Sequence, entry.Sequence), new SessionSequence(2), [entry.Id]);
        var candidate = Candidate("   ", entry.Sequence, new SessionSequence(2));

        var result = await validator.ValidateAsync(
            new CompactionValidationRequest(
                TestFactory.Request(source.Context, _branchId, new SessionVersion(1), entry.Sequence), source, cut, candidate),
            TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<CompactionValidationRejected>();
        rejected.Issues.ShouldContain(i => i.Kind == CompactionValidationIssueKind.InvalidStructure);
    }

    [Fact]
    public async Task ValidateAsync_WhenCoveredEntryMissingFromSource_ReturnsMissingSourceIssue()
    {
        var validator = CreateValidator();
        var address = Address();
        var entry = TestFactory.MessageEntry(address, _branchId, 1, new string('x', 1000));
        var source = Source([entry]);
        var missingId = new SessionEntryId(Guid.NewGuid());
        var cut = new CompactionCut(
            new CompactionSourceRange(entry.Sequence, entry.Sequence), new SessionSequence(2), [entry.Id, missingId]);
        var candidate = Candidate("short summary", entry.Sequence, new SessionSequence(2));

        var result = await validator.ValidateAsync(
            new CompactionValidationRequest(
                TestFactory.Request(source.Context, _branchId, new SessionVersion(1), entry.Sequence), source, cut, candidate),
            TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<CompactionValidationRejected>();
        var issue = rejected.Issues.Single(i => i.Kind == CompactionValidationIssueKind.MissingSource);
        issue.SourceEntryIds.ShouldContain(missingId);
    }

    [Fact]
    public async Task ValidateAsync_WhenCutSplitsCausalPair_ReturnsBrokenCausalityIssue()
    {
        var validator = CreateValidator();
        var address = Address();
        var (call, result) = TestFactory.ToolCallPair(address, _branchId, 1, 2);
        var source = Source([call, result]);

        // A malformed cut that covers the call but leaves the result retained.
        var cut = new CompactionCut(
            new CompactionSourceRange(call.Sequence, call.Sequence), result.Sequence, [call.Id]);
        var candidate = Candidate(new string('y', 2000), call.Sequence, result.Sequence);

        var validation = await validator.ValidateAsync(
            new CompactionValidationRequest(
                TestFactory.Request(source.Context, _branchId, new SessionVersion(2), result.Sequence), source, cut, candidate),
            TestContext.Current.CancellationToken);

        var rejected = validation.ShouldBeOfType<CompactionValidationRejected>();
        rejected.Issues.ShouldContain(i => i.Kind == CompactionValidationIssueKind.BrokenCausality);
    }

    [Fact]
    public async Task ValidateAsync_WhenCheckpointExceedsMaximumCharacters_ReturnsUnboundedContentIssue()
    {
        var validator = CreateValidator(maximumCheckpointCharacters: 10);
        var address = Address();
        var entry = TestFactory.MessageEntry(address, _branchId, 1, new string('x', 1000));
        var source = Source([entry]);
        var cut = new CompactionCut(
            new CompactionSourceRange(entry.Sequence, entry.Sequence), new SessionSequence(2), [entry.Id]);
        var candidate = Candidate(new string('y', 50), entry.Sequence, new SessionSequence(2));

        var result = await validator.ValidateAsync(
            new CompactionValidationRequest(
                TestFactory.Request(source.Context, _branchId, new SessionVersion(1), entry.Sequence), source, cut, candidate),
            TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<CompactionValidationRejected>();
        rejected.Issues.ShouldContain(i => i.Kind == CompactionValidationIssueKind.UnboundedContent);
    }

    [Fact]
    public async Task ValidateAsync_WhenReductionBelowMinimum_ReturnsNonReducingIssue()
    {
        var validator = CreateValidator();
        var address = Address();
        var entry = TestFactory.MessageEntry(address, _branchId, 1, "short");
        var source = Source([entry]);
        var cut = new CompactionCut(
            new CompactionSourceRange(entry.Sequence, entry.Sequence), new SessionSequence(2), [entry.Id]);

        // Summary as long as the source: no measurable reduction.
        var candidate = Candidate("short", entry.Sequence, new SessionSequence(2));

        var result = await validator.ValidateAsync(
            new CompactionValidationRequest(
                TestFactory.Request(
                    source.Context, _branchId, new SessionVersion(1), entry.Sequence, minimumReductionRatio: 0.5),
                source,
                cut,
                candidate),
            TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<CompactionValidationRejected>();
        _ = rejected.Issues.Single(i => i.Kind == CompactionValidationIssueKind.NonReducing);
    }

    private SessionAddress Address() => new(_agentId, _sessionId);

    private CompactionSourceSnapshot Source(ImmutableArray<SessionEntry> entries)
    {
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        return new CompactionSourceSnapshot(
            context,
            _branchId,
            new SessionVersion(entries.Length),
            entries.IsEmpty ? new SessionSequence(0) : entries[^1].Sequence,
            entries);
    }

    private static CompactionCandidate Candidate(string summaryText, SessionSequence coveredThrough, SessionSequence retainedStart)
    {
        var checkpoint = new CompactionCheckpoint(
            [new TextPart(summaryText, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var manifest = new CompactionManifest(
            new CompactionManifestId(Guid.NewGuid()),
            TestFactory.CompactionContext(),
            new BranchId(Guid.NewGuid()),
            new SessionVersion(1),
            new CompactionSourceRange(new SessionSequence(1), coveredThrough),
            retainedStart,
            new CompactionProducer(ExtractiveCompactionStrategy.StrategyKey, deterministic: true, ExtensionData.Empty),
            new ContextEpoch(0),
            new CompactionSizeEstimate(100, 400, 1),
            new CompactionSizeEstimate(10, 40, 1),
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);

        return new CompactionCandidate(manifest, checkpoint);
    }

    private static CharacterCompactionSizeEstimator CreateEstimator(double charactersPerToken = 4.0) =>
        new(Options.Create(new CompactionOptions { CharactersPerToken = charactersPerToken }));

    private static DefaultCompactionValidator CreateValidator(
        double charactersPerToken = 4.0, int maximumCheckpointCharacters = 16_000)
    {
        var options = Options.Create(new CompactionOptions
        {
            CharactersPerToken = charactersPerToken,
            MaximumCheckpointCharacters = maximumCheckpointCharacters
        });
        return new DefaultCompactionValidator(new CharacterCompactionSizeEstimator(options), options);
    }
}
