// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Captures deterministic authorization evidence for facade composition tests.</summary>
internal sealed class TestSecurityProfileSelector: ISecurityProfileSelector
{
    /// <summary>Gets every exact capture request received by this selector.</summary>
    public List<SecurityAuthorizationCaptureRequest> Requests { get; } = [];

    /// <summary>Gets or sets the profile version placed in captured evidence.</summary>
    public SecurityProfileVersion ProfileVersion { get; set; } = new(1);

    /// <summary>Gets or sets a caller token source cancelled immediately before capture returns.</summary>
    public CancellationTokenSource? CancellationSource { get; set; }

    /// <inheritdoc/>
    public ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(
        SecurityAuthorizationCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        CancellationSource?.Cancel();
        return ValueTask.FromResult<SecurityAuthorizationCaptureResult>(new SecurityAuthorizationCaptured(
            new SecurityAuthorizationContext(
                request.ProfileKey,
                ProfileVersion,
                new SecurityPolicySnapshotReference(
                    new SecurityPolicySnapshotId(Guid.Parse("d0000000-0000-0000-0000-000000000004")),
                    new SecurityPolicyVersion(1),
                    new ContentHash("sha256:test-policy")),
                new ComponentKey<ISecurityAuthority>("authority"),
                request.AgentDefinitionRevision,
                request.ConfigurationVersion,
                request.Scope,
                request.Identity)));
    }
}
