// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Foundation;

using System.Reflection;

using AgentKit;

/// <summary>Verifies AgentErrorCodes behavior and contracts.</summary>
public sealed class AgentErrorCodesTests
{
    [Fact]
    public void AgentErrorCodes_WhenEnumerated_PublishEveryUniqueDocumentedMachineValue()
    {
        string[] expected = ["Unknown", "InvalidInput", "Conflict", "InvalidState", "SessionBusy", "CorruptState", "InvalidConfiguration", "UntrustedConfiguration", "MissingDependency", "UnsupportedCapability", "IncompatibleModel", "IncompatibleSchema", "AuthenticationFailed", "CredentialUnavailable", "AuthorizationDenied", "ApprovalRequired", "ApprovalExpired", "InvalidProviderRequest", "RateLimited", "ProviderUnavailable", "ContentFiltered", "Timeout", "ConnectionFailed", "ProtocolViolation", "TruncatedStream", "UnknownTool", "InvalidToolArguments", "ToolFailed", "ToolInterrupted", "ToolOutcomeUnknown", "OutputValidationFailed", "StructuredOutputMissing", "RequestLimit", "TokenLimit", "CostLimit", "ToolLimit", "ContextLimit", "QueueCapacity", "StoreUnavailable", "ConcurrencyConflict", "MigrationRequired", "RecoveryIncompatible", "LeaseLost", "ReconciliationRequired", "Cancelled", "ObserverFailed", "ExtensionFailed", "InternalFailure",];
        var properties = typeof(AgentErrorCodes).GetProperties(BindingFlags.Public | BindingFlags.Static);
        var actual = properties.Select(property => ((AgentErrorCode) property.GetValue(null)!).Value!).ToArray();
        actual.ShouldBe(expected, ignoreOrder: true);
        actual.Distinct(StringComparer.Ordinal).Count().ShouldBe(actual.Length);
        properties.ShouldAllBe(property => property.SetMethod == null);
    }
}
