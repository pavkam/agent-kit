// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>Resolves literal instruction sources in deterministic priority order.</summary>
internal sealed class DefaultInstructionResolver: IInstructionResolver
{
    /// <inheritdoc/>
    public ValueTask<InstructionResolutionResult> ResolveAsync(
        InstructionResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Sources.IsEmpty)
        {
            return ValueTask.FromResult<InstructionResolutionResult>(new InstructionResolutionResolved([]));
        }

        var ordered = request.Sources
            .OrderByDescending(static source => source.Priority)
            .ThenBy(static source => source.Source.Key.Value, StringComparer.Ordinal)
            .ToArray();

        var builder = ImmutableArray.CreateBuilder<AgentMessage>();
        foreach (var source in ordered)
        {
            if (source is not LiteralInstructionSource literal)
            {
                return ValueTask.FromResult<InstructionResolutionResult>(new InstructionResolutionFailed(
                    new ContextPreparationFailure(
                        ContextPreparationFailureKind.Unknown,
                        "An instruction source kind is not supported by the default resolver.",
                        ExtensionData.Empty)));
            }

            foreach (var message in literal.Messages)
            {
                var failure = ValidateInstructionMessage(message);
                if (failure is not null)
                {
                    return ValueTask.FromResult<InstructionResolutionResult>(new InstructionResolutionFailed(failure));
                }

                builder.Add(message);
            }
        }

        return ValueTask.FromResult<InstructionResolutionResult>(new InstructionResolutionResolved(builder.ToImmutable()));
    }

    private static ContextPreparationFailure? ValidateInstructionMessage(AgentMessage message)
    {
        return message.State != MessageState.Complete
            ? new ContextPreparationFailure(
                ContextPreparationFailureKind.InvalidInstructionMessage,
                $"An instruction message's state is {message.State}, but only complete messages may be sent to a provider.",
                ExtensionData.Empty)
            : message is SystemMessage or DeveloperMessage
                ? null
                : new ContextPreparationFailure(
                    ContextPreparationFailureKind.InvalidInstructionMessage,
                    "An instruction message is not a system or developer message; only those roles may carry instruction authority.",
                    ExtensionData.Empty);
    }
}
