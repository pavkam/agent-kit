// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Publishes the framework's stable portable error-code declarations as typed immutable values.</summary>
/// <remarks>
/// The declarations are neither a parser nor a registry. Unknown custom codes remain valid <see cref="AgentErrorCode"/>
/// values and preserve their exact ordinal text. Taxonomy groupings do not add runtime category inference.
/// </remarks>
public static class AgentErrorCodes
{
    /// <summary>Gets the code for a failure whose portable category is not known.</summary>
    /// <value>The exact machine code <c>Unknown</c>.</value>
    public static AgentErrorCode Unknown { get; } = new("Unknown");

    /// <summary>Gets the code for caller input that fails the owning operation contract before work begins.</summary>
    /// <value>The exact machine code <c>InvalidInput</c>.</value>
    public static AgentErrorCode InvalidInput { get; } = new("InvalidInput");

    /// <summary>Gets the code for a requested semantic change that conflicts with current state.</summary>
    /// <value>The exact machine code <c>Conflict</c>.</value>
    public static AgentErrorCode Conflict { get; } = new("Conflict");

    /// <summary>Gets the code for state that cannot participate in the requested transition.</summary>
    /// <value>The exact machine code <c>InvalidState</c>.</value>
    public static AgentErrorCode InvalidState { get; } = new("InvalidState");

    /// <summary>Gets the code for a session that cannot admit the operation under its concurrency rules.</summary>
    /// <value>The exact machine code <c>SessionBusy</c>.</value>
    public static AgentErrorCode SessionBusy { get; } = new("SessionBusy");

    /// <summary>Gets the code for authoritative state that fails integrity or structural validation.</summary>
    /// <value>The exact machine code <c>CorruptState</c>.</value>
    public static AgentErrorCode CorruptState { get; } = new("CorruptState");

    /// <summary>Gets the code for configured values that fail their declared schema or invariants.</summary>
    /// <value>The exact machine code <c>InvalidConfiguration</c>.</value>
    public static AgentErrorCode InvalidConfiguration { get; } = new("InvalidConfiguration");

    /// <summary>Gets the code for configuration rejected because its provenance lacks required trust.</summary>
    /// <value>The exact machine code <c>UntrustedConfiguration</c>.</value>
    public static AgentErrorCode UntrustedConfiguration { get; } = new("UntrustedConfiguration");

    /// <summary>Gets the code for a required composed capability or retained version that is unavailable.</summary>
    /// <value>The exact machine code <c>MissingDependency</c>.</value>
    public static AgentErrorCode MissingDependency { get; } = new("MissingDependency");

    /// <summary>Gets the code for a requested behavior the selected component does not provide.</summary>
    /// <value>The exact machine code <c>UnsupportedCapability</c>.</value>
    public static AgentErrorCode UnsupportedCapability { get; } = new("UnsupportedCapability");

    /// <summary>Gets the code for a selected model whose capabilities cannot satisfy the request.</summary>
    /// <value>The exact machine code <c>IncompatibleModel</c>.</value>
    public static AgentErrorCode IncompatibleModel { get; } = new("IncompatibleModel");

    /// <summary>Gets the code for a schema whose dialect or assertions cannot be used by the selected engine.</summary>
    /// <value>The exact machine code <c>IncompatibleSchema</c>.</value>
    public static AgentErrorCode IncompatibleSchema { get; } = new("IncompatibleSchema");

    /// <summary>Gets the code for credentials or identity evidence that could not be authenticated.</summary>
    /// <value>The exact machine code <c>AuthenticationFailed</c>.</value>
    public static AgentErrorCode AuthenticationFailed { get; } = new("AuthenticationFailed");

    /// <summary>Gets the code for required credential material that could not be obtained.</summary>
    /// <value>The exact machine code <c>CredentialUnavailable</c>.</value>
    public static AgentErrorCode CredentialUnavailable { get; } = new("CredentialUnavailable");

    /// <summary>Gets the code for a protected operation refused by the selected authority.</summary>
    /// <value>The exact machine code <c>AuthorizationDenied</c>.</value>
    public static AgentErrorCode AuthorizationDenied { get; } = new("AuthorizationDenied");

    /// <summary>Gets the code for a protected operation awaiting an explicit approval decision.</summary>
    /// <value>The exact machine code <c>ApprovalRequired</c>.</value>
    public static AgentErrorCode ApprovalRequired { get; } = new("ApprovalRequired");

    /// <summary>Gets the code for an approval that elapsed before it could authorize the operation.</summary>
    /// <value>The exact machine code <c>ApprovalExpired</c>.</value>
    public static AgentErrorCode ApprovalExpired { get; } = new("ApprovalExpired");

    /// <summary>Gets the code for a provider-bound request rejected before or by provider validation.</summary>
    /// <value>The exact machine code <c>InvalidProviderRequest</c>.</value>
    public static AgentErrorCode InvalidProviderRequest { get; } = new("InvalidProviderRequest");

    /// <summary>Gets the code for provider or host capacity temporarily refusing additional work.</summary>
    /// <value>The exact machine code <c>RateLimited</c>.</value>
    public static AgentErrorCode RateLimited { get; } = new("RateLimited");

    /// <summary>Gets the code for a selected provider operation that cannot currently serve the request.</summary>
    /// <value>The exact machine code <c>ProviderUnavailable</c>.</value>
    public static AgentErrorCode ProviderUnavailable { get; } = new("ProviderUnavailable");

    /// <summary>Gets the code for content declined by a provider safety or moderation boundary.</summary>
    /// <value>The exact machine code <c>ContentFiltered</c>.</value>
    public static AgentErrorCode ContentFiltered { get; } = new("ContentFiltered");

    /// <summary>Gets the code for an operation that failed to settle within its effective deadline.</summary>
    /// <value>The exact machine code <c>Timeout</c>.</value>
    public static AgentErrorCode Timeout { get; } = new("Timeout");

    /// <summary>Gets the code for a transport connection that could not be established or maintained.</summary>
    /// <value>The exact machine code <c>ConnectionFailed</c>.</value>
    public static AgentErrorCode ConnectionFailed { get; } = new("ConnectionFailed");

    /// <summary>Gets the code for wire or streaming behavior that violated the selected protocol contract.</summary>
    /// <value>The exact machine code <c>ProtocolViolation</c>.</value>
    public static AgentErrorCode ProtocolViolation { get; } = new("ProtocolViolation");

    /// <summary>Gets the code for a response stream that ended before its required terminal state.</summary>
    /// <value>The exact machine code <c>TruncatedStream</c>.</value>
    public static AgentErrorCode TruncatedStream { get; } = new("TruncatedStream");

    /// <summary>Gets the code for a requested alias that did not resolve in the captured tool catalog.</summary>
    /// <value>The exact machine code <c>UnknownTool</c>.</value>
    public static AgentErrorCode UnknownTool { get; } = new("UnknownTool");

    /// <summary>Gets the code for tool arguments rejected by bounds, parsing, schema, or semantic validation.</summary>
    /// <value>The exact machine code <c>InvalidToolArguments</c>.</value>
    public static AgentErrorCode InvalidToolArguments { get; } = new("InvalidToolArguments");

    /// <summary>Gets the code for a resolved tool invocation that returned or mapped a definite failure.</summary>
    /// <value>The exact machine code <c>ToolFailed</c>.</value>
    public static AgentErrorCode ToolFailed { get; } = new("ToolFailed");

    /// <summary>Gets the code for a tool invocation stopped before a complete terminal outcome was available.</summary>
    /// <value>The exact machine code <c>ToolInterrupted</c>.</value>
    public static AgentErrorCode ToolInterrupted { get; } = new("ToolInterrupted");

    /// <summary>Gets the code for a tool effect whose terminal outcome cannot be determined safely.</summary>
    /// <value>The exact machine code <c>ToolOutcomeUnknown</c>.</value>
    public static AgentErrorCode ToolOutcomeUnknown { get; } = new("ToolOutcomeUnknown");

    /// <summary>Gets the code for a model candidate that failed the selected output contract.</summary>
    /// <value>The exact machine code <c>OutputValidationFailed</c>.</value>
    public static AgentErrorCode OutputValidationFailed { get; } = new("OutputValidationFailed");

    /// <summary>Gets the code for a response that omitted the structured value required by its output contract.</summary>
    /// <value>The exact machine code <c>StructuredOutputMissing</c>.</value>
    public static AgentErrorCode StructuredOutputMissing { get; } = new("StructuredOutputMissing");

    /// <summary>Gets the code for a configured count or request-volume ceiling that would be exceeded.</summary>
    /// <value>The exact machine code <c>RequestLimit</c>.</value>
    public static AgentErrorCode RequestLimit { get; } = new("RequestLimit");

    /// <summary>Gets the code for a token capacity or accounting ceiling that would be exceeded.</summary>
    /// <value>The exact machine code <c>TokenLimit</c>.</value>
    public static AgentErrorCode TokenLimit { get; } = new("TokenLimit");

    /// <summary>Gets the code for a monetary capacity or accounting ceiling that would be exceeded.</summary>
    /// <value>The exact machine code <c>CostLimit</c>.</value>
    public static AgentErrorCode CostLimit { get; } = new("CostLimit");

    /// <summary>Gets the code for a tool-call capacity or accounting ceiling that would be exceeded.</summary>
    /// <value>The exact machine code <c>ToolLimit</c>.</value>
    public static AgentErrorCode ToolLimit { get; } = new("ToolLimit");

    /// <summary>Gets the code for mandatory or selected context that cannot fit within configured bounds.</summary>
    /// <value>The exact machine code <c>ContextLimit</c>.</value>
    public static AgentErrorCode ContextLimit { get; } = new("ContextLimit");

    /// <summary>Gets the code for a queue that cannot admit more work under its capacity policy.</summary>
    /// <value>The exact machine code <c>QueueCapacity</c>.</value>
    public static AgentErrorCode QueueCapacity { get; } = new("QueueCapacity");

    /// <summary>Gets the code for an authoritative persistence operation whose selected store is unavailable.</summary>
    /// <value>The exact machine code <c>StoreUnavailable</c>.</value>
    public static AgentErrorCode StoreUnavailable { get; } = new("StoreUnavailable");

    /// <summary>Gets the code for a persistence revision, lease, fence, or atomic update precondition that failed.</summary>
    /// <value>The exact machine code <c>ConcurrencyConflict</c>.</value>
    public static AgentErrorCode ConcurrencyConflict { get; } = new("ConcurrencyConflict");

    /// <summary>Gets the code for retained data that requires an explicit compatibility migration before use.</summary>
    /// <value>The exact machine code <c>MigrationRequired</c>.</value>
    public static AgentErrorCode MigrationRequired { get; } = new("MigrationRequired");

    /// <summary>Gets the code for recovery evidence that cannot be interpreted by the selected retained contracts.</summary>
    /// <value>The exact machine code <c>RecoveryIncompatible</c>.</value>
    public static AgentErrorCode RecoveryIncompatible { get; } = new("RecoveryIncompatible");

    /// <summary>Gets the code for work that no longer owns the lease or fence required to publish safely.</summary>
    /// <value>The exact machine code <c>LeaseLost</c>.</value>
    public static AgentErrorCode LeaseLost { get; } = new("LeaseLost");

    /// <summary>Gets the code for uncertain durable or external work that requires owner reconciliation.</summary>
    /// <value>The exact machine code <c>ReconciliationRequired</c>.</value>
    public static AgentErrorCode ReconciliationRequired { get; } = new("ReconciliationRequired");

    /// <summary>Gets the code for work stopped by its owning cancellation contract.</summary>
    /// <value>The exact machine code <c>Cancelled</c>.</value>
    public static AgentErrorCode Cancelled { get; } = new("Cancelled");

    /// <summary>Gets the code for required observation delivery that could not be completed safely.</summary>
    /// <value>The exact machine code <c>ObserverFailed</c>.</value>
    public static AgentErrorCode ObserverFailed { get; } = new("ObserverFailed");

    /// <summary>Gets the code for an extension or hook whose execution failed under its point policy.</summary>
    /// <value>The exact machine code <c>ExtensionFailed</c>.</value>
    public static AgentErrorCode ExtensionFailed { get; } = new("ExtensionFailed");

    /// <summary>Gets the code for an unexpected framework invariant or implementation failure.</summary>
    /// <value>The exact machine code <c>InternalFailure</c>.</value>
    public static AgentErrorCode InternalFailure { get; } = new("InternalFailure");

}
