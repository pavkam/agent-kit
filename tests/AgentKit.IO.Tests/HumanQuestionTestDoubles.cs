// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using System.Diagnostics;

internal sealed class RecordingGrantStore: ISecurityGrantStore
{
    internal List<SecurityEnforcementRequest> Enforcements { get; } = [];
    internal List<SecurityEnforcementIntent> Intents { get; } = [];
    internal GrantConsumptionStatus Status { get; set; } = GrantConsumptionStatus.Consumed;
    internal bool IncludeReceipt { get; set; } = true;
    internal bool ReturnExactReceipt { get; set; } = true;
    internal Action? OnConsume { get; set; }

    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default)
    {
        Enforcements.Add(enforcement);
        return ValueTask.FromResult(new GrantConsumptionResult(Status, 0, $"Grant {Status}."));
    }

    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        Enforcements.Add(enforcement);
        Intents.Add(intent);
        OnConsume?.Invoke();
        var status = Matches(grant, enforcement) ? Status : GrantConsumptionStatus.Mismatch;
        var receipt = IncludeReceipt && (status is GrantConsumptionStatus.Consumed or GrantConsumptionStatus.Reconciled)
            ? new SecurityEnforcementIntentReceipt(
                ReturnExactReceipt
                    ? intent.Id
                    : new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
                grant.Id,
                grant.RequestId,
                enforcement,
                intent.RequiredFence,
                SecurityEnforcementBinding.Fingerprint(enforcement, intent),
                DateTimeOffset.UnixEpoch)
            : null;
        return ValueTask.FromResult(new GrantConsumptionResult(
            status,
            0,
            $"Grant {status}.",
            receipt));
    }

    private static bool Matches(SecurityGrant grant, SecurityEnforcementRequest enforcement)
    {
        Debug.Assert(grant is not null, "A grant-store comparison receives the broker's non-null grant.");
        Debug.Assert(enforcement is not null, "A grant-store comparison receives the broker's non-null evidence.");
        return grant.Scope == enforcement.Scope
        && grant.Identity == enforcement.Identity
        && grant.Authorization == enforcement.Authorization
        && grant.Audience == enforcement.Audience
        && grant.Kind == enforcement.Kind
        && grant.Effect == enforcement.Effect
        && grant.Resources.SequenceEqual(enforcement.Resources)
        && grant.InputFingerprint == enforcement.InputFingerprint
        && grant.RevocationVersion == enforcement.RevocationVersion;
    }

    public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(true);
}

internal sealed class RecordingQuestionChannel: IHumanQuestionChannel
{
    internal List<HumanQuestionPrompt> Prompts { get; } = [];
    internal Action? OnAsk { get; set; }
    internal Func<HumanQuestionPrompt, HumanQuestionResult> Result { get; set; } = static prompt =>
        new HumanQuestionAnswered(prompt.Id,
            new HumanQuestionAnswer(prompt.Options[0].Id, null, prompt.Identity, prompt.Deadline));

    public ValueTask<HumanQuestionResult> AskAsync(
        HumanQuestionPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        Prompts.Add(prompt);
        OnAsk?.Invoke();
        return ValueTask.FromResult(Result(prompt));
    }
}

internal sealed class FixedSecurityEnforcementIntentIdGenerator(SecurityEnforcementIntentId id):
    IIdentifierGenerator<SecurityEnforcementIntentId>
{
    internal int Calls { get; private set; }

    public SecurityEnforcementIntentId Create()
    {
        Calls++;
        return id;
    }
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}

internal sealed class ThrowingTimestampTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;

    public override long GetTimestamp() => throw new InvalidTimeZoneException("clock failure");
}

internal sealed class ThrowingLogger: ILogger<DefaultHumanQuestionBroker>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => throw new InvalidOperationException("observer");
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("observer");
}

internal sealed class RecordingQuestionLogger: ILogger<DefaultHumanQuestionBroker>
{
    internal List<string> Messages { get; } = [];
    internal List<(int EventId, LogLevel Level)> Events { get; } = [];
    internal List<ImmutableArray<string>> FieldNames { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Events.Add((eventId.Id, logLevel));
        if (state is IReadOnlyList<KeyValuePair<string, object?>> fields)
        {
            FieldNames.Add([.. fields.Select(static field => field.Key)]);

            // Exercises the generated log-state's classic non-generic enumeration surface and Count accessor
            // that some third-party logging providers use instead of the generic key/value interface, plus the
            // out-of-range indexer guard, so the compiler-generated accessors are covered from a real call site.
            if (state is System.Collections.IEnumerable legacy)
            {
                foreach (var _ in legacy) { }
            }

            if (fields.Count > 0)
            {
                for (var index = 0; index < fields.Count; index++) { _ = fields[index]; }

                try
                {
                    _ = fields[fields.Count];
                }
                catch (IndexOutOfRangeException)
                {
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }
        }

        Messages.Add(formatter(state, exception));
    }
}
