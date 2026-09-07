// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

/// <summary>An <see cref="IOutputValidator"/> test double that returns a scripted result and records every request it received.</summary>
internal sealed class FakeOutputValidator: IOutputValidator
{
    private readonly Func<OutputValidationRequest, OutputValidationResult> _handler;

    public FakeOutputValidator(string name, Func<OutputValidationRequest, OutputValidationResult> handler)
    {
        Name = name;
        _handler = handler;
    }

    public string Name { get; }

    public List<OutputValidationRequest> ReceivedRequests { get; } = [];

    public ValueTask<OutputValidationResult> ValidateAsync(
        OutputValidationRequest request, CancellationToken cancellationToken = default)
    {
        ReceivedRequests.Add(request);
        return ValueTask.FromResult(_handler(request));
    }
}
