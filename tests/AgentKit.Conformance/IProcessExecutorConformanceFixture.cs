// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Adapter-specific setup for shared process executor contract scenarios.</summary>
public interface IProcessExecutorConformanceFixture: IAsyncDisposable
{
    /// <summary>Gets the executable resolver paired with the executor.</summary>
    public IExecutableResolver Resolver { get; }

    /// <summary>Gets the process executor under test.</summary>
    public IProcessExecutor Executor { get; }

    /// <summary>Builds one canonical start request accepted by the fixture profile.</summary>
    /// <returns>A complete unresolved start request.</returns>
    public ProcessStartRequest CreateStartRequest();

    /// <summary>Builds one authorized grant matching the resolved start facts for <paramref name="request"/>.</summary>
    /// <param name="request">The unresolved start request.</param>
    /// <returns>A grant registered for consumption by the executor audience.</returns>
    public ValueTask<SecurityGrant> CreateAuthorizedGrantAsync(ProcessStartRequest request);
}
