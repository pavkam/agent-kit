// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Is the first-party <see cref="IAgentMessageChannel"/>: it admits a message as steering or follow-up input through the recipient agent's public engine surface.</summary>
/// <remarks>
/// <para>
/// The channel resolves the recipient through <see cref="AgentEngine.GetAgentAsync"/> and admits the message with
/// <see cref="Agent.SteerAsync"/> or <see cref="Agent.FollowUpAsync"/>, so it is authorized, queued, ordered, and bounded
/// like any other input and never starts a run. The input identity derives deterministically from the sender, the recipient
/// session, and the idempotency key, which makes a retried send resolve to the original admission and a reused key with different
/// content a conflict. The sender, recipient, causal goal and attempt, and idempotency key travel as input extension data, so the
/// recipient's record shows where the message came from; that data is evidence, never an instruction.
/// </para>
/// <para>The engine is resolved lazily on first use to avoid a constructor cycle, and the channel holds no state between messages.</para>
/// </remarks>
public sealed class EngineAgentMessageChannel: IAgentMessageChannel
{
    private readonly IServiceProvider _services;
    private readonly ILogger<EngineAgentMessageChannel> _logger;
    private AgentEngine? _engine;

    /// <summary>Initializes the channel.</summary>
    /// <param name="services">The root provider the hosting engine is resolved from on first use.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public EngineAgentMessageChannel(IServiceProvider services, ILogger<EngineAgentMessageChannel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _logger = logger ?? NullLogger<EngineAgentMessageChannel>.Instance;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The hosting engine has been disposed.</exception>
    public async ValueTask<AgentMessageResult> SendAsync(AgentMessageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.AgentMessageSend,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.AgentId, request.SenderAgentId.ToString()),
                new(AgentKitTagNames.SessionId, request.RecipientSessionId.ToString()),
                new(AgentKitTagNames.RunId, request.SenderRunId.ToString()),
                new(AgentKitTagNames.TenantId, request.Identity.TenantId.Value),
            ]);
        try
        {
            var result = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
            var outcome = result switch
            {
                AgentMessageAccepted => "accepted",
                AgentMessageRejected rejected => Stable(rejected.Kind),
                _ => "unknown",
            };
            WorkerObservation.Safe(() =>
            {
                if (result is AgentMessageAccepted)
                {
                    scope.Activity.SetSuccessful(outcome);
                }
                else
                {
                    scope.Activity.SetFailed(outcome, outcome);
                }
            });
            WorkerObservation.Safe(() => GoalWorkerMetrics.RecordMessage(outcome));
            WorkerObservation.Safe(() =>
            {
                if (result is AgentMessageAccepted accepted)
                {
                    GoalWorkerLog.MessageAdmitted(_logger, request.SenderAgentId, request.RecipientSessionId, accepted.Receipt.Existing);
                }
                else if (result is AgentMessageRejected rejected)
                {
                    GoalWorkerLog.MessageRejected(_logger, request.SenderAgentId, request.RecipientSessionId, rejected.Kind);
                }
            });
            return result;
        }
        catch (OperationCanceledException)
        {
            WorkerObservation.Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            WorkerObservation.Safe(() => GoalWorkerMetrics.RecordMessage("cancelled"));
            throw;
        }
    }

    private async ValueTask<AgentMessageResult> SendCoreAsync(AgentMessageRequest request, CancellationToken cancellationToken)
    {
        var engine = _engine ??= _services.GetRequiredService<AgentEngine>();
        var resolution = await engine.GetAgentAsync(request.RecipientAgentId, cancellationToken).ConfigureAwait(false);
        if (resolution is not ResolvedAgent { Agent: var recipient })
        {
            return new AgentMessageRejected(AgentMessageRejectionKind.UnknownRecipient, "The recipient agent is not hosted by this engine.");
        }

        var input = new AgentInput(InputIdFor(request), request.Delivery, request.Parts, Evidence(request));
        InputAdmissionResult admitted;
        try
        {
            admitted = request.Delivery == InputDelivery.Steer
                ? await recipient.SteerAsync(request.RecipientSessionId, request.Identity, input, cancellationToken: cancellationToken).ConfigureAwait(false)
                : await recipient.FollowUpAsync(request.RecipientSessionId, request.Identity, input, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (AgentAdmissionRejectedException exception)
        {
            return new AgentMessageRejected(AgentMessageRejectionKind.Rejected, exception.Rejection.Reason);
        }

        return admitted switch
        {
            AcceptedInput accepted => new AgentMessageAccepted(accepted.Receipt),
            InputConflict => new AgentMessageRejected(AgentMessageRejectionKind.Conflict, "The idempotency key was already used for a different message."),
            QueueCapacityExceeded => new AgentMessageRejected(AgentMessageRejectionKind.CapacityExceeded, "The recipient session cannot accept more pending input."),
            RejectedInput rejected => new AgentMessageRejected(AgentMessageRejectionKind.Rejected, rejected.Rejection.SafeReason),
            _ => new AgentMessageRejected(AgentMessageRejectionKind.Rejected, "The recipient did not admit the message."),
        };
    }

    private static string Stable(AgentMessageRejectionKind kind) => kind switch
    {
        AgentMessageRejectionKind.UnknownRecipient => "unknown_recipient",
        AgentMessageRejectionKind.Conflict => "conflict",
        AgentMessageRejectionKind.CapacityExceeded => "capacity_exceeded",
        AgentMessageRejectionKind.Rejected => "rejected",
        _ => "rejected",
    };

    private static InputId InputIdFor(AgentMessageRequest request)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"agentkit.message:{request.SenderAgentId}:{request.RecipientSessionId}:{request.IdempotencyKey.Value}"));
        return new InputId(new Guid(hash.AsSpan(0, 16)));
    }

    private static ExtensionData Evidence(AgentMessageRequest request)
    {
        var values = ImmutableDictionary<string, ExtensionValue>.Empty
            .Add("agentkit.message.sender_agent", Value(request.SenderAgentId.ToString()))
            .Add("agentkit.message.sender_session", Value(request.SenderSessionId.ToString()))
            .Add("agentkit.message.sender_run", Value(request.SenderRunId.ToString()))
            .Add("agentkit.message.recipient_agent", Value(request.RecipientAgentId.ToString()))
            .Add("agentkit.message.idempotency_key", Value(request.IdempotencyKey.Value))
            .Add("agentkit.message.instruction_authority", Value(false));
        if (request.GoalId is { } goal)
        {
            values = values.Add("agentkit.message.goal", Value(goal.ToString()));
        }

        if (request.AttemptId is { } attempt)
        {
            values = values.Add("agentkit.message.attempt", Value(attempt.ToString()));
        }

        return new ExtensionData(values);
    }

    private static ExtensionValue Value<T>(T value) => new([.. JsonSerializer.SerializeToUtf8Bytes(value)]);
}
