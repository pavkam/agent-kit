// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Operating-system <see cref="IFileReader"/> bound to one keyed profile snapshot.</summary>
internal sealed class OperatingSystemFileReader(
    ISecurityGrantStore grantStore,
    ISecurityAuditDispatcher auditDispatcher,
    IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
    IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
    TimeProvider timeProvider,
    OperatingSystemFileSystemOptionsSnapshot options): IFileReader
{
    private readonly ISecurityGrantStore _grantStore = grantStore;
    private readonly ISecurityAuditDispatcher _auditDispatcher = auditDispatcher;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds = auditRecordIds;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds = intentIds;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly OperatingSystemFileSystemOptionsSnapshot _options = options;

    /// <summary>Gets the security audience for this reader profile.</summary>
    internal ComponentId SecurityAudience { get; } = new($"agentkit.filesystem.os.{options.ProfileKey.Value}");

    /// <inheritdoc/>
    public async ValueTask<FileReadOpenResult> OpenReadAsync(
        AuthorizedFileRead operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            return await OperatingSystemFileOperations.OpenReadAsync(
                operation,
                _grantStore,
                _auditDispatcher,
                _auditRecordIds,
                _intentIds,
                _timeProvider,
                _options,
                SecurityAudience,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new FileReadOpenCancelled();
        }
    }
}
