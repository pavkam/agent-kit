// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Operating-system <see cref="IFileWriter"/> bound to one keyed profile snapshot.</summary>
internal sealed class OperatingSystemFileWriter(
    ISecurityGrantStore grantStore,
    ISecurityAuditDispatcher auditDispatcher,
    IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
    IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
    TimeProvider timeProvider,
    OperatingSystemFileSystemOptionsSnapshot options): IFileWriter
{
    private readonly ISecurityGrantStore _grantStore = grantStore;
    private readonly ISecurityAuditDispatcher _auditDispatcher = auditDispatcher;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds = auditRecordIds;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds = intentIds;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly OperatingSystemFileSystemOptionsSnapshot _options = options;

    /// <summary>Gets the security audience for this writer profile.</summary>
    internal ComponentId SecurityAudience { get; } = new($"agentkit.filesystem.os.{options.ProfileKey.Value}");

    /// <inheritdoc/>
    public ValueTask<FileWriteResult> WriteAsync(
        AuthorizedFileWrite operation,
        FileWriteContent content,
        CancellationToken cancellationToken = default) =>
        OperatingSystemFileWriteOperations.WriteAsync(
            operation,
            content,
            _grantStore,
            _auditDispatcher,
            _auditRecordIds,
            _intentIds,
            _timeProvider,
            _options,
            SecurityAudience,
            cancellationToken);
}
