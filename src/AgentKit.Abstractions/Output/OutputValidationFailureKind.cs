// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The category of a failure encountered while processing one output candidate.</summary>
public enum OutputValidationFailureKind
{
    /// <summary>The definition's <see cref="OutputMode"/> is not yet supported by this processor.</summary>
    UnsupportedMode,

    /// <summary>The definition requires a schema for its mode, but none was declared.</summary>
    MissingSchema,

    /// <summary>The candidate exceeds the configured maximum candidate size.</summary>
    OversizedCandidate,

    /// <summary>The candidate could not be parsed as JSON.</summary>
    MalformedJson,

    /// <summary>The candidate did not validate against the declared schema.</summary>
    SchemaValidationFailed,

    /// <summary>The candidate validated against its schema but could not be deserialized into the declared runtime type.</summary>
    DeserializationFailed,

    /// <summary>An ordered output validator rejected the candidate.</summary>
    ValidatorFailed,

    /// <summary>The definition references a validator name with no matching registration.</summary>
    ValidatorNotFound,

    /// <summary>Every allowed validation-retry attempt has been exhausted.</summary>
    RetryAttemptsExhausted,

    /// <summary>An unclassified failure occurred during processing.</summary>
    Unknown
}
