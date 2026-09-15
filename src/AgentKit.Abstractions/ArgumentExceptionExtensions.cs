// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Runtime.CompilerServices;

/// <summary>
/// Reusable <see cref="ArgumentException"/> guard clauses for constraints the
/// base class library does not already expose as a <c>ThrowIf*</c> member.
/// </summary>
/// <remarks>
/// AgentKit's argument-validation convention is to use the BCL's own
/// <c>ArgumentException.ThrowIf*</c>/<c>ArgumentNullException.ThrowIf*</c>/
/// <c>ArgumentOutOfRangeException.ThrowIf*</c> guard-clause methods at every
/// public and internal boundary, called through the exception type itself
/// (for example, <c>ArgumentException.ThrowIfNullOrWhiteSpace(value)</c>).
/// When a genuinely reusable constraint has no matching BCL member — such as
/// rejecting a default, uninitialized <see cref="ImmutableArray{T}"/> — this
/// class adds exactly one canonical extension for it, following the same
/// calling convention, rather than letting every call site repeat the
/// condition inline or invent a competing helper.
/// </remarks>
public static class ArgumentExceptionExtensions
{
    extension(ArgumentException)
    {
        /// <summary>Throws when a path is not lexically rooted for the current platform.</summary>
        /// <param name="path">The non-blank path to inspect without filesystem observation.</param>
        /// <param name="paramName">The path parameter name inferred from the call site when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="path"/> is not an absolute lexical path.</exception>
        public static void ThrowIfPathNotRooted(
            string path,
            [CallerArgumentExpression(nameof(path))] string? paramName = null)
        {
            if (!Path.IsPathRooted(path))
            {
                throw new ArgumentException("The path must be absolute.", paramName);
            }
        }

        /// <summary>Throws when run-profile publications contain a null item or duplicate agent-definition coordinates.</summary>
        /// <param name="publications">The initialized immutable publication sequence to inspect.</param>
        /// <param name="paramName">The sequence parameter name inferred from the call site when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="publications"/> is default, contains null, or repeats an agent and definition-revision pair.</exception>
        public static void ThrowIfDuplicateAgentRunProfileCoordinates(
            ImmutableArray<AgentRunProfilePublication> publications,
            [CallerArgumentExpression(nameof(publications))] string? paramName = null)
        {
            ArgumentException.ThrowIfContainsNull(publications, paramName);
            var coordinates = new HashSet<(AgentId, AgentDefinitionRevision)>();
            foreach (var publication in publications)
            {
                if (!coordinates.Add((
                    publication.SecurityProfile.AgentId,
                    publication.SecurityProfile.AgentDefinitionRevision)))
                {
                    throw new ArgumentException(
                        "Run-profile publications must have unique agent and definition-revision coordinates.",
                        paramName);
                }
            }
        }

        /// <summary>Throws when an audit event-kind sequence is uninitialized, contains an undefined kind, or declares one kind more than once.</summary>
        /// <param name="eventKinds">The initialized audit event-kind sequence to inspect.</param>
        /// <param name="paramName">The parameter attributed to a duplicate event kind.</param>
        /// <exception cref="ArgumentException"><paramref name="eventKinds"/> is a default, uninitialized immutable array or contains a duplicate event kind.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventKinds"/> contains an undefined <see cref="SecurityAuditEventKind"/>.</exception>
        public static void ThrowIfDuplicateSecurityAuditEventKind(
            ImmutableArray<SecurityAuditEventKind> eventKinds,
            [CallerArgumentExpression(nameof(eventKinds))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefault(eventKinds, paramName);
            var seen = new HashSet<SecurityAuditEventKind>();
            foreach (var eventKind in eventKinds)
            {
                ArgumentOutOfRangeException.ThrowIfUndefined(eventKind, paramName);
                if (!seen.Add(eventKind))
                {
                    throw new ArgumentException("Security audit event kinds must be unique.", paramName);
                }
            }
        }

        /// <summary>
        /// Throws when a codec's readable schema declaration is uninitialized,
        /// contains a default or duplicate schema version, or omits its write
        /// version.
        /// </summary>
        /// <param name="readableVersions">
        /// The initialized ordered schema-version sequence to validate.
        /// </param>
        /// <param name="writeVersion">
        /// The nondefault write schema that must appear once in
        /// <paramref name="readableVersions"/>.
        /// </param>
        /// <param name="paramName">
        /// The readable-version parameter name inferred from the caller when
        /// omitted.
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="readableVersions"/> is default, repeats a schema
        /// version, or omits <paramref name="writeVersion"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="writeVersion"/> is default, or
        /// <paramref name="readableVersions"/> contains a default schema
        /// version.
        /// </exception>
        public static void ThrowIfInvalidSessionEntryCodecReadableVersions(
            ImmutableArray<SchemaVersion> readableVersions,
            SchemaVersion writeVersion,
            [CallerArgumentExpression(nameof(readableVersions))] string? paramName = null)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(writeVersion, default);
            ArgumentException.ThrowIfDefault(readableVersions, paramName);
            var seen = new HashSet<SchemaVersion>();
            foreach (var version in readableVersions)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(version, default, paramName);
                if (!seen.Add(version))
                {
                    throw new ArgumentException("Readable schema versions must be unique.", paramName);
                }
            }

            if (!seen.Contains(writeVersion))
            {
                throw new ArgumentException("Readable schema versions must include the write schema version.", paramName);
            }
        }

        /// <summary>Throws when two values that describe one structural relation differ.</summary>
        /// <typeparam name="T">The compared value type.</typeparam>
        /// <param name="actual">The caller-supplied value that must match.</param>
        /// <param name="expected">The authoritative expected value.</param>
        /// <param name="paramName">The actual-value parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="actual"/> and <paramref name="expected"/> are not equal under <see cref="EqualityComparer{T}.Default"/>.</exception>
        public static void ThrowIfNotEqual<T>(
            T actual,
            T expected,
            [CallerArgumentExpression(nameof(actual))] string? paramName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
            {
                throw new ArgumentException("Value must match the related request evidence.", paramName);
            }
        }

        /// <summary>Throws when an unsuccessful grant-consumption status carries a permission-to-start receipt.</summary>
        /// <param name="status">The terminal grant-consumption status.</param>
        /// <param name="receipt">The optional enforcement-intent receipt.</param>
        /// <param name="paramName">The receipt parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="receipt"/> is non-null and <paramref name="status"/> is neither newly consumed nor reconciled.</exception>
        public static void ThrowIfInvalidIntentReceipt(
            GrantConsumptionStatus status,
            SecurityEnforcementIntentReceipt? receipt,
            [CallerArgumentExpression(nameof(receipt))] string? paramName = null)
        {
            if (status is not (GrantConsumptionStatus.Consumed or GrantConsumptionStatus.Reconciled)
                && receipt is not null)
            {
                throw new ArgumentException(
                    "Only a newly consumed or reconciled result may carry an enforcement-intent receipt.",
                    paramName);
            }
        }

        /// <summary>Throws when an immutable identity sequence is default, empty, or contains a default or duplicate identity.</summary>
        /// <typeparam name="T">The non-nullable value identity type.</typeparam>
        /// <param name="values">The candidate ordered identity sequence.</param>
        /// <param name="paramName">The sequence parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="values"/> is default or empty, or contains a default or duplicate value.</exception>
        public static void ThrowIfDefaultEmptyOrDuplicate<T>(
            ImmutableArray<T> values,
            [CallerArgumentExpression(nameof(values))] string? paramName = null)
            where T : struct
        {
            ArgumentException.ThrowIfDefaultOrEmpty(values, paramName);
            var seen = new HashSet<T>();
            foreach (var value in values)
            {
                if (EqualityComparer<T>.Default.Equals(value, default) || !seen.Add(value))
                {
                    throw new ArgumentException("Identity values must be initialized and unique.", paramName);
                }
            }
        }

        /// <summary>Throws when a session operation context does not identify one execution lane.</summary>
        /// <param name="context">The non-null session context to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="context"/> has no execution lane.</exception>
        public static void ThrowIfSessionContextNotLaneBound(
            SessionOperationContext context,
            [CallerArgumentExpression(nameof(context))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(context, paramName);
            if (context.ExecutionLaneId is null)
            {
                throw new ArgumentException("The session operation must identify an execution lane.", paramName);
            }
        }

        /// <summary>Throws when a session operation context is not a lane-bound in-run operation.</summary>
        /// <param name="context">The non-null session context to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="context"/> has no execution lane or does not carry <see cref="InRunOperationCorrelation"/>.</exception>
        public static void ThrowIfSessionContextNotInRun(
            SessionOperationContext context,
            [CallerArgumentExpression(nameof(context))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(context, paramName);
            if (context.ExecutionLaneId is null || context.Correlation is not InRunOperationCorrelation)
            {
                throw new ArgumentException("The session operation must identify a lane and carry in-run correlation.", paramName);
            }
        }

        /// <summary>Throws when a session operation context is not a before-run operation.</summary>
        /// <param name="context">The non-null session context to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="context"/> does not carry <see cref="BeforeRunOperationCorrelation"/>.</exception>
        public static void ThrowIfSessionContextNotBeforeRun(
            SessionOperationContext context,
            [CallerArgumentExpression(nameof(context))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(context, paramName);
            if (context.Correlation is not BeforeRunOperationCorrelation)
            {
                throw new ArgumentException("The session operation must carry before-run correlation.", paramName);
            }
        }

        /// <summary>Throws when captured authority cannot describe session creation before a session identity exists.</summary>
        /// <param name="authorization">The non-null captured authorization context to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is null.</exception>
        /// <exception cref="ArgumentException">The scope already identifies a session or does not carry <see cref="BeforeRunOperationCorrelation"/>.</exception>
        public static void ThrowIfInvalidSessionCreationAuthorization(
            SecurityAuthorizationContext authorization,
            [CallerArgumentExpression(nameof(authorization))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(authorization, paramName);
            if (authorization.Scope.SessionId is not null
                || authorization.Scope.Correlation is not BeforeRunOperationCorrelation)
            {
                throw new ArgumentException(
                    "Session creation authorization must be sessionless and carry before-run correlation.",
                    paramName);
            }
        }

        /// <summary>Throws when an initialized immutable sequence does not contain a required value.</summary>
        /// <typeparam name="T">The equatable value type carried by the sequence.</typeparam>
        /// <param name="values">The initialized immutable sequence to inspect.</param>
        /// <param name="required">The value that must occur.</param>
        /// <param name="paramName">The parameter name inferred from the sequence expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="values"/> is default or does not contain <paramref name="required"/>.</exception>
        public static void ThrowIfDoesNotContain<T>(
            ImmutableArray<T> values,
            T required,
            [CallerArgumentExpression(nameof(values))] string? paramName = null)
            where T : IEquatable<T>
        {
            ArgumentException.ThrowIfDefault(values, paramName);
            if (!values.Contains(required))
            {
                throw new ArgumentException("The sequence must contain the required value.", paramName);
            }
        }

        /// <summary>Throws when a budget address cannot bind the supplied identity and operation correlation.</summary>
        /// <param name="address">The non-null budget address to validate.</param>
        /// <param name="identity">The non-null authenticated execution identity.</param>
        /// <param name="correlation">The non-null causal operation correlation.</param>
        /// <param name="paramName">The address parameter name attributed to a binding mismatch.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="address"/>, <paramref name="identity"/>, or <paramref name="correlation"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The address differs from the identity, operation, or active-run stage described by the correlation.
        /// </exception>
        public static void ThrowIfInvalidBudgetExecutionBinding(
            BudgetScopeAddress address,
            ExecutionIdentity identity,
            OperationCorrelation correlation,
            [CallerArgumentExpression(nameof(address))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(address);
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(correlation);

            if (address.TenantId != identity.TenantId
                || address.PrincipalId != identity.PrincipalId
                || address.OperationId is not { } operationId
                || operationId != correlation.OperationId
                || (correlation is BeforeRunOperationCorrelation or AfterRunOperationCorrelation
                    && address.RunId is not null)
                || (correlation is InRunOperationCorrelation inRun && address.RunId != inRun.RunId))
            {
                throw new ArgumentException("Budget address must exactly bind execution identity and active correlation.", paramName);
            }
        }
        /// <summary>Throws when a durable operation address and captured context do not name one exact in-run or after-run operation.</summary>
        /// <param name="address">The durable operation coordinates to validate.</param>
        /// <param name="executionContext">The captured durability and full authorization context to validate.</param>
        /// <param name="paramName">The parameter attributed to inconsistent captured context.</param>
        /// <exception cref="ArgumentNullException"><paramref name="address"/> or <paramref name="executionContext"/> is null.</exception>
        /// <exception cref="ArgumentException">The scope differs from the address or uses before-run/sessionless correlation that the current address cannot represent.</exception>
        public static void ThrowIfInvalidDurableOperationBinding(
            DurableOperationAddress address,
            DurableExecutionContext executionContext,
            [CallerArgumentExpression(nameof(executionContext))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(address);
            ArgumentNullException.ThrowIfNull(executionContext);
            var scope = executionContext.Authorization.Scope;
            if (scope.SessionId is not { } sessionId)
            {
                throw new ArgumentException("The current durable address cannot represent sessionless authorization.", paramName);
            }

            ArgumentException.ThrowIfNotEqual(address.AgentId, scope.AgentId, paramName);
            ArgumentException.ThrowIfNotEqual(address.SessionId, sessionId, paramName);
            ArgumentException.ThrowIfNotEqual(address.OperationId, scope.Correlation.OperationId, paramName);
            switch (scope.Correlation)
            {
                case InRunOperationCorrelation inRun:
                    ArgumentException.ThrowIfNotEqual(address.RunId, inRun.RunId, paramName);
                    ArgumentException.ThrowIfNotEqual(address.TurnId, inRun.TurnId, paramName);
                    return;
                case AfterRunOperationCorrelation afterRun:
                    ArgumentException.ThrowIfNotEqual(address.RunId, afterRun.CausalRunId, paramName);
                    ArgumentException.ThrowIfNotEqual(address.TurnId, null, paramName);
                    return;
                default:
                    throw new ArgumentException("The current durable address requires in-run or after-run authorization correlation.", paramName);
            }
        }

        /// <summary>Throws when a recovery evidence checkpoint belongs to a different durable binding.</summary>
        /// <param name="binding">The non-null exact binding retained by the recovery evidence.</param>
        /// <param name="latestCheckpoint">The optional checkpoint that must retain structurally equal coordinates and captured context.</param>
        /// <param name="paramName">The parameter attributed to a checkpoint that does not belong to <paramref name="binding"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="binding"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">A supplied <paramref name="latestCheckpoint"/> has a different address, durability selection, authorization selection, scope, or authenticated identity.</exception>
        public static void ThrowIfRecoveryEvidenceCheckpointDoesNotMatchBinding(
            DurableOperationBinding binding,
            DurableCheckpoint? latestCheckpoint,
            [CallerArgumentExpression(nameof(latestCheckpoint))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(binding);

            if (latestCheckpoint is not null && latestCheckpoint.Binding != binding)
            {
                throw new ArgumentException("The checkpoint must retain the recovery evidence binding exactly.", paramName);
            }
        }

        /// <summary>Throws when an output-schema profile has inconsistent dialect or vocabulary sets.</summary>
        /// <param name="defaultDialect">The default dialect that must be one supported dialect.</param>
        /// <param name="supportedDialects">The initialized, non-empty unique dialect set.</param>
        /// <param name="assertionKeywords">The initialized unique assertion-keyword set.</param>
        /// <param name="annotationKeywords">The initialized unique annotation-keyword set disjoint from assertions.</param>
        /// <param name="defaultDialectParamName">The parameter name attributed to an invalid default dialect.</param>
        /// <param name="supportedDialectsParamName">The parameter name attributed to invalid supported dialects.</param>
        /// <param name="assertionKeywordsParamName">The parameter name attributed to invalid assertions.</param>
        /// <param name="annotationKeywordsParamName">The parameter name attributed to invalid annotations.</param>
        /// <exception cref="ArgumentException">A set is default, empty where required, contains blank or duplicate values, overlaps another vocabulary set, or omits its default dialect.</exception>
        public static void ThrowIfInvalidOutputSchemaProfile(
            JsonSchemaDialectId defaultDialect,
            ImmutableArray<JsonSchemaDialectId> supportedDialects,
            ImmutableArray<string> assertionKeywords,
            ImmutableArray<string> annotationKeywords,
            [CallerArgumentExpression(nameof(defaultDialect))] string? defaultDialectParamName = null,
            [CallerArgumentExpression(nameof(supportedDialects))] string? supportedDialectsParamName = null,
            [CallerArgumentExpression(nameof(assertionKeywords))] string? assertionKeywordsParamName = null,
            [CallerArgumentExpression(nameof(annotationKeywords))] string? annotationKeywordsParamName = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(defaultDialect.Value, defaultDialectParamName);
            ArgumentException.ThrowIfDefaultOrEmpty(supportedDialects, supportedDialectsParamName);
            ArgumentException.ThrowIfDefault(assertionKeywords, assertionKeywordsParamName);
            ArgumentException.ThrowIfDefault(annotationKeywords, annotationKeywordsParamName);
            var dialects = new HashSet<JsonSchemaDialectId>();
            foreach (var dialect in supportedDialects)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(dialect.Value, supportedDialectsParamName);
                if (!dialects.Add(dialect))
                {
                    throw new ArgumentException("Supported dialects must be unique.", supportedDialectsParamName);
                }
            }

            if (!dialects.Contains(defaultDialect))
            {
                throw new ArgumentException("The default dialect must be supported.", defaultDialectParamName);
            }

            ValidateKeywords(assertionKeywords, assertionKeywordsParamName);
            ValidateKeywords(annotationKeywords, annotationKeywordsParamName);
            if (assertionKeywords.Any(annotationKeywords.Contains))
            {
                throw new ArgumentException("Assertion and annotation keywords must not overlap.", annotationKeywordsParamName);
            }

            static void ValidateKeywords(ImmutableArray<string> keywords, string? parameterName)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var keyword in keywords)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(keyword, parameterName);
                    if (!seen.Add(keyword))
                    {
                        throw new ArgumentException("Schema keywords must be unique.", parameterName);
                    }
                }
            }
        }

        /// <summary>Throws when a dialect is not declared by the supplied schema-engine profile.</summary>
        /// <param name="profile">The non-null immutable engine profile.</param>
        /// <param name="dialect">The initialized dialect that must be supported.</param>
        /// <param name="paramName">The dialect parameter name inferred from the call site when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="profile"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="dialect"/> is default or is absent from <paramref name="profile"/>.
        /// </exception>
        public static void ThrowIfUnsupportedOutputSchemaDialect(
            OutputSchemaEngineProfile profile,
            JsonSchemaDialectId dialect,
            [CallerArgumentExpression(nameof(dialect))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentException.ThrowIfNullOrWhiteSpace(dialect.Value, paramName);
            if (!profile.SupportedDialects.Contains(dialect))
            {
                throw new ArgumentException("The dialect must be supported by the profile.", paramName);
            }
        }

        /// <summary>Throws when registered agent-definition sources reuse a source identity.</summary>
        /// <param name="sources">The initialized, non-null source collection to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="sources"/> is default, contains <see langword="null"/>, contains an invalid source identity, or contains duplicate source identities.</exception>
        public static void ThrowIfDuplicateAgentDefinitionSourceIds(
            ImmutableArray<IAgentDefinitionSource> sources,
            [CallerArgumentExpression(nameof(sources))] string? paramName = null)
        {
            ArgumentException.ThrowIfContainsNull(sources, paramName);
            var seen = new HashSet<AgentDefinitionSourceId>();
            foreach (var source in sources)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceId.Value, paramName);
                if (!seen.Add(source.SourceId))
                {
                    throw new ArgumentException(
                        $"Value must not contain duplicate definition source id '{source.SourceId}'.",
                        paramName);
                }
            }
        }

        /// <summary>Throws when bootstrap snapshots reuse a definition-source identity.</summary>
        /// <param name="snapshots">The initialized, non-null bootstrap snapshots to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="snapshots"/> is default, contains <see langword="null"/>, or contains duplicate source identities.</exception>
        public static void ThrowIfDuplicateAgentDefinitionSnapshotSourceIds(
            ImmutableArray<AgentDefinitionSourceSnapshot> snapshots,
            [CallerArgumentExpression(nameof(snapshots))] string? paramName = null)
        {
            ArgumentException.ThrowIfContainsNull(snapshots, paramName);
            var seen = new HashSet<AgentDefinitionSourceId>();
            foreach (var snapshot in snapshots)
            {
                if (!seen.Add(snapshot.SourceId))
                {
                    throw new ArgumentException(
                        $"Value must not contain duplicate definition source id '{snapshot.SourceId}'.",
                        paramName);
                }
            }
        }

        /// <summary>Throws when a bootstrap snapshot names no registered definition source.</summary>
        /// <param name="snapshots">The initialized, non-null bootstrap snapshots to inspect.</param>
        /// <param name="sources">The initialized, non-null registered sources that bound the bootstrap set.</param>
        /// <param name="snapshotsParamName">The parameter name inferred from the snapshot expression when omitted.</param>
        /// <param name="sourcesParamName">The parameter name inferred from the source expression when omitted.</param>
        /// <exception cref="ArgumentException">Either collection is default or contains <see langword="null"/>, a source has an invalid identity, or a snapshot names an identity absent from <paramref name="sources"/>.</exception>
        public static void ThrowIfUnknownAgentDefinitionSource(
            ImmutableArray<AgentDefinitionSourceSnapshot> snapshots,
            ImmutableArray<IAgentDefinitionSource> sources,
            [CallerArgumentExpression(nameof(snapshots))] string? snapshotsParamName = null,
            [CallerArgumentExpression(nameof(sources))] string? sourcesParamName = null)
        {
            ArgumentException.ThrowIfContainsNull(snapshots, snapshotsParamName);
            ArgumentException.ThrowIfContainsNull(sources, sourcesParamName);
            var sourceIds = sources.Select(static source => source.SourceId).ToHashSet();
            foreach (var sourceId in sourceIds)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(sourceId.Value, sourcesParamName);
            }

            if (snapshots.Any(snapshot => !sourceIds.Contains(snapshot.SourceId)))
            {
                throw new ArgumentException(
                    "A bootstrap snapshot names an unregistered definition source.",
                    snapshotsParamName);
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> if <paramref name="array"/>
        /// is a default, uninitialized <see cref="ImmutableArray{T}"/>.
        /// </summary>
        /// <remarks>
        /// A default <see cref="ImmutableArray{T}"/> has no backing storage
        /// at all: <see cref="ImmutableArray{T}.IsDefault"/> is
        /// <see langword="true"/>, and enumerating or indexing it throws a
        /// confusing <see cref="NullReferenceException"/> far away from
        /// where the bad value actually originated. AgentKit's collection
        /// parameters must always be either a genuinely populated array or
        /// the explicit <see cref="ImmutableArray{T}.Empty"/> sentinel, so
        /// this guard turns that mistake into an immediate, clearly
        /// attributed argument error at the constructor boundary instead.
        /// </remarks>
        /// <typeparam name="T">The array element type.</typeparam>
        /// <param name="array">The candidate array.</param>
        /// <param name="paramName">
        /// The name of the validated parameter. Inferred automatically from
        /// the call-site expression via
        /// <see cref="CallerArgumentExpressionAttribute"/> when not
        /// supplied explicitly.
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="array"/> is a default, uninitialized array.
        /// </exception>
        public static void ThrowIfDefault<T>(
            ImmutableArray<T> array,
            [CallerArgumentExpression(nameof(array))] string? paramName = null)
        {
            if (array.IsDefault)
            {
                throw new ArgumentException(
                    "Value must not be a default ImmutableArray<T>. Use ImmutableArray<T>.Empty.",
                    paramName);
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> if <paramref name="array"/>
        /// is default or contains no elements.
        /// </summary>
        /// <typeparam name="T">The array element type.</typeparam>
        /// <param name="array">The candidate array that must contain at least one element.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="array"/> is default or empty.</exception>
        public static void ThrowIfDefaultOrEmpty<T>(
            ImmutableArray<T> array,
            [CallerArgumentExpression(nameof(array))] string? paramName = null)
        {
            if (array.IsDefaultOrEmpty)
            {
                throw new ArgumentException("Value must contain at least one element.", paramName);
            }
        }

        /// <summary>Throws an <see cref="ArgumentException"/> if an immutable reference array is default or contains null.</summary>
        /// <typeparam name="T">The non-null reference element type.</typeparam>
        /// <param name="array">The candidate array.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="array"/> is default or contains a null element.</exception>
        public static void ThrowIfContainsNull<T>(
            ImmutableArray<T> array,
            [CallerArgumentExpression(nameof(array))] string? paramName = null)
            where T : class
        {
            ArgumentException.ThrowIfDefault(array, paramName);
            if (array.Any(static value => value is null))
            {
                throw new ArgumentException("Value must not contain null elements.", paramName);
            }
        }

        /// <summary>Throws when authentication evidence names an issuer different from its containing assertion.</summary>
        /// <param name="evidence">The non-null authentication evidence to inspect.</param>
        /// <param name="issuer">The assertion issuer that the evidence must match.</param>
        /// <param name="paramName">The parameter name attributed to the invalid evidence.</param>
        /// <exception cref="ArgumentNullException"><paramref name="evidence"/> is null.</exception>
        /// <exception cref="ArgumentException">The evidence and assertion issuer identities differ.</exception>
        public static void ThrowIfIssuerMismatch(
            AuthenticationEvidence evidence,
            IdentityIssuerId issuer,
            [CallerArgumentExpression(nameof(evidence))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(evidence, paramName);
            if (evidence.Issuer != issuer)
            {
                throw new ArgumentException("Authentication evidence must be issued by the assertion issuer.", paramName);
            }
        }

        /// <summary>Throws when a delegation chain crosses the child identity's tenant boundary.</summary>
        /// <param name="chain">The initialized, non-null delegation links to inspect.</param>
        /// <param name="tenantId">The child tenant every ancestor link must retain.</param>
        /// <param name="paramName">The parameter name attributed to the invalid chain.</param>
        /// <exception cref="ArgumentException"><paramref name="chain"/> is default, contains null, or contains a link for another tenant.</exception>
        public static void ThrowIfCrossesTenant(
            ImmutableArray<DelegationIdentityLink> chain,
            TenantId tenantId,
            [CallerArgumentExpression(nameof(chain))] string? paramName = null)
        {
            ArgumentException.ThrowIfContainsNull(chain, paramName);
            if (chain.Any(link => link.TenantId != tenantId))
            {
                throw new ArgumentException("Every delegation ancestor must remain within the child identity's tenant.", paramName);
            }
        }

        /// <summary>Throws when requested delegated claims are not a subset of the parent's normalized claims.</summary>
        /// <param name="claims">The initialized, non-null requested claims.</param>
        /// <param name="parentClaims">The initialized parent claim set that bounds delegation.</param>
        /// <param name="paramName">The parameter name attributed to an invalid requested claim.</param>
        /// <exception cref="ArgumentException">Either array is default, contains null, or a requested claim is absent from the parent.</exception>
        public static void ThrowIfNotSubsetOf(
            ImmutableArray<IdentityClaim> claims,
            ImmutableArray<IdentityClaim> parentClaims,
            [CallerArgumentExpression(nameof(claims))] string? paramName = null)
        {
            ArgumentException.ThrowIfContainsNull(claims, paramName);
            ArgumentException.ThrowIfContainsNull(parentClaims, nameof(parentClaims));
            if (claims.Any(claim => !parentClaims.Contains(claim)))
            {
                throw new ArgumentException("Every delegated claim must be present in the parent identity.", paramName);
            }
        }

        /// <summary>Throws an <see cref="ArgumentException"/> if a string contains the NUL character.</summary>
        /// <param name="value">The non-null candidate string.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="value"/> contains a NUL character.</exception>
        public static void ThrowIfContainsNul(
            string value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(value, paramName);
            if (value.Contains('\0', StringComparison.Ordinal))
            {
                throw new ArgumentException("Value must not contain NUL characters.", paramName);
            }
        }

        /// <summary>Throws when a host is not a canonicalizable DNS name or unscoped IP literal.</summary>
        /// <param name="value">The candidate host text.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/> is blank, contains a scoped-address marker, or is not a DNS/IP host.
        /// </exception>
        public static void ThrowIfInvalidNetworkHost(
            string value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
            var candidate = value.Trim().TrimEnd('.');
            if (candidate.Contains('%', StringComparison.Ordinal)
                || Uri.CheckHostName(candidate) == UriHostNameType.Unknown)
            {
                throw new ArgumentException("Value must be a DNS name or an unscoped IP-address literal.", paramName);
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> if <paramref name="uri"/>
        /// is not an absolute URI.
        /// </summary>
        /// <param name="uri">The candidate URI.</param>
        /// <param name="paramName">
        /// The name of the validated parameter, inferred from the call-site
        /// expression when omitted.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="uri"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="uri"/> is not absolute.</exception>
        public static void ThrowIfNotAbsoluteUri(
            Uri uri,
            [CallerArgumentExpression(nameof(uri))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(uri, paramName);

            if (!uri.IsAbsoluteUri)
            {
                throw new ArgumentException("Value must be an absolute URI.", paramName);
            }
        }

        /// <summary>Throws when a web-search result URL is not credential-free absolute HTTP(S).</summary>
        /// <param name="uri">The absolute URI to validate.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="uri"/> is null.</exception>
        /// <exception cref="ArgumentException">The scheme is not HTTP(S), the host is missing, or user information is present.</exception>
        public static void ThrowIfInvalidWebResultUri(
            Uri uri,
            [CallerArgumentExpression(nameof(uri))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(uri, paramName);
            if (!uri.IsAbsoluteUri
                || uri.Scheme is not ("http" or "https")
                || string.IsNullOrWhiteSpace(uri.Host)
                || !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new ArgumentException(
                    "Value must be an absolute credential-free HTTP(S) URI with a host.",
                    paramName);
            }
        }

        /// <summary>Throws when a configured web-search destination is not a network endpoint resource.</summary>
        /// <param name="resource">The resource to validate.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="resource"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="resource"/> is not a network endpoint.</exception>
        public static void ThrowIfNotNetworkEndpointResource(
            ProtectedResource resource,
            [CallerArgumentExpression(nameof(resource))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(resource, paramName);
            if (resource.Kind != ProtectedResourceKind.NetworkEndpoint)
            {
                throw new ArgumentException("Value must identify a network endpoint.", paramName);
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> if <paramref name="path"/>
        /// is not a non-rooted relative URI path.
        /// </summary>
        /// <remarks>
        /// Rooted, authority-relative, and absolute values are rejected so
        /// resolving the path against a trusted base address cannot replace
        /// that address's origin or base path.
        /// </remarks>
        /// <param name="path">The candidate relative URI path.</param>
        /// <param name="paramName">
        /// The name of the validated parameter, inferred from the call-site
        /// expression when omitted.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="path"/> is empty, whitespace, rooted, authority-relative,
        /// or an absolute URI.
        /// </exception>
        public static void ThrowIfNotRelativeUriPath(
            string path,
            [CallerArgumentExpression(nameof(path))] string? paramName = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path, paramName);

            if (!Uri.TryCreate(path, UriKind.Relative, out _) || path[0] is '/' or '\\')
            {
                throw new ArgumentException("Value must be a non-rooted relative URI path.", paramName);
            }
        }

        /// <summary>Throws when human-question options contain duplicate identities.</summary>
        /// <param name="options">The initialized option array to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="options"/> is default or contains duplicate identities.</exception>
        public static void ThrowIfDuplicateQuestionOptionIds(
            ImmutableArray<HumanQuestionOption> options,
            [CallerArgumentExpression(nameof(options))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefault(options, paramName);
            if (options.Select(static option => option.Id).Distinct().Count() != options.Length)
            {
                throw new ArgumentException("Question option identities must be unique.", paramName);
            }
        }

        /// <summary>Throws when work-plan items contain duplicate stable identities.</summary>
        /// <param name="items">The initialized item array to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="items"/> is default or contains duplicate identities.</exception>
        public static void ThrowIfDuplicatePlanItemIds(
            ImmutableArray<WorkPlanItem> items,
            [CallerArgumentExpression(nameof(items))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefault(items, paramName);
            if (items.Select(static item => item.Id).Distinct().Count() != items.Length)
            {
                throw new ArgumentException("Plan item identities must be unique.", paramName);
            }
        }

        /// <summary>Throws when more than one work-plan item is in progress.</summary>
        /// <param name="items">The initialized item array to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="items"/> is default or contains multiple in-progress items.</exception>
        public static void ThrowIfMultipleInProgressPlanItems(
            ImmutableArray<WorkPlanItem> items,
            [CallerArgumentExpression(nameof(items))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefault(items, paramName);
            if (items.Count(static item => item.Status == PlanItemStatus.InProgress) > 1)
            {
                throw new ArgumentException("At most one plan item may be in progress.", paramName);
            }
        }

        /// <summary>Throws when requests do not form one valid atomic budget reservation batch.</summary>
        /// <param name="requests">The initialized, non-empty ordered requests to validate.</param>
        /// <param name="scopeId">The budget scope that must own every request.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="requests"/> is default or empty, contains a null
        /// request, targets another scope or operation, contains duplicate item
        /// idempotency keys, or expresses one dimension in incompatible units.
        /// </exception>
        public static void ThrowIfInvalidBudgetReservationBatch(
            ImmutableArray<BudgetReservationRequest> requests,
            BudgetScopeId scopeId,
            [CallerArgumentExpression(nameof(requests))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefaultOrEmpty(requests, paramName);
            ArgumentException.ThrowIfContainsNull(requests, paramName);
            var operationId = requests[0].OperationId;
            if (requests.Any(request => request.ScopeId != scopeId))
            {
                throw new ArgumentException("Every request must target the receiving budget scope.", paramName);
            }

            if (requests.Any(request => request.OperationId != operationId))
            {
                throw new ArgumentException("Every request must name the same logical operation.", paramName);
            }

            if (requests.Select(static request => request.IdempotencyKey).Distinct().Count() != requests.Length)
            {
                throw new ArgumentException("Batch item idempotency keys must be unique.", paramName);
            }

            if (requests.GroupBy(static request => request.Dimension)
                .Any(static group => group.Select(static request => request.Unit).Distinct().Skip(1).Any()))
            {
                throw new ArgumentException("Every dimension in the batch must use one compatible unit.", paramName);
            }
        }

        /// <summary>Throws when scope limits cannot form one deterministic per-dimension limit set.</summary>
        /// <param name="limits">The initialized limit sequence to validate in its declared order.</param>
        /// <param name="paramName">The parameter name inferred from the limit expression when omitted.</param>
        /// <exception cref="ArgumentNullException">A member has a default dimension or unit.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="limits"/> is default, contains a null member, contains blank dimension or unit text,
        /// or repeats a dimension.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// A limit value is negative or a limit kind is undefined.
        /// </exception>
        public static void ThrowIfInvalidBudgetScopeLimits(
            ImmutableArray<BudgetLimit> limits,
            [CallerArgumentExpression(nameof(limits))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefault(limits, paramName);
            ArgumentException.ThrowIfContainsNull(limits, paramName);
            var dimensions = new HashSet<BudgetDimension>();
            foreach (var limit in limits)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(limit.Dimension.Value, paramName);
                ArgumentOutOfRangeException.ThrowIfNegative(limit.Value, paramName);
                ArgumentException.ThrowIfNullOrWhiteSpace(limit.Unit.Value, paramName);
                ArgumentOutOfRangeException.ThrowIfUndefined(limit.Kind, paramName);
                if (!dimensions.Add(limit.Dimension))
                {
                    throw new ArgumentException(
                        "A budget scope may declare at most one limit for each dimension.", paramName);
                }
            }
        }

        /// <summary>Throws when a stream cannot supply artifact content.</summary>
        /// <param name="stream">The non-null stream to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="stream"/> is not readable.</exception>
        public static void ThrowIfNotReadable(
            Stream stream,
            [CallerArgumentExpression(nameof(stream))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(stream, paramName);
            if (!stream.CanRead)
            {
                throw new ArgumentException("Stream must be readable.", paramName);
            }
        }

        /// <summary>Throws when deferred requests cannot form one coherent terminal external handoff.</summary>
        /// <param name="requests">The initialized, nonnull, unique external requests to validate.</param>
        /// <param name="allowEmpty">Whether an empty initialized collection is permitted.</param>
        /// <param name="paramName">The parameter name inferred from the request collection expression when omitted.</param>
        /// <exception cref="ArgumentNullException">A request is null.</exception>
        /// <exception cref="ArgumentException">The collection is uninitialized, disallowed empty, duplicate, runtime-owned, or has inconsistent session/run correlation.</exception>
        public static void ThrowIfInvalidExternalDeferrals(ImmutableArray<DeferredOperationRequest> requests, bool allowEmpty = false,
            [CallerArgumentExpression(nameof(requests))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefault(requests, paramName);
            ArgumentException.ThrowIfNotEqual(allowEmpty || !requests.IsEmpty, true, paramName);
            HashSet<DeferredRequestId> ids = [];
            DeferredOperationRequest? first = null;
            foreach (var request in requests)
            {
                ArgumentNullException.ThrowIfNull(request, paramName);
                ArgumentException.ThrowIfNotEqual(request.ContinuationOwner, DeferralContinuationOwner.ExternalWorkflow, paramName);
                ArgumentException.ThrowIfNotEqual(ids.Add(request.Id), true, paramName);
                first ??= request;
                ArgumentException.ThrowIfNotEqual(request.SessionId, first.SessionId, paramName);
                ArgumentException.ThrowIfNotEqual(request.RunId, first.RunId, paramName);
            }
        }

        /// <summary>Throws when an outcome cannot support successful continuation completion.</summary>
        /// <param name="outcome">The non-null semantic run outcome to classify.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="outcome"/> is neither successful output completion nor idle completion.</exception>
        public static void ThrowIfNotSuccessfulRunOutcome(
            AgentRunOutcome outcome,
            [CallerArgumentExpression(nameof(outcome))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(outcome, paramName);
            if (outcome is not AgentRunCompleted and not AgentRunIdle and not RunSucceeded and not RunIdle)
            {
                throw new ArgumentException("Outcome must represent successful output or idle completion.", paramName);
            }

            if (outcome is AgentRunCompleted completed
                && (completed.FinalMessage is not { State: MessageState.Complete }
                    || completed.FinalMessage.RunId is not { } runId
                    || runId == default
                    || completed.FinalMessage.TurnId is not { } turnId
                    || turnId == default))
            {
                throw new ArgumentException("Successful output completion requires a complete message with initialized run and turn identities.", paramName);
            }
        }

        /// <summary>Throws when a halt proposal is given a successful semantic outcome.</summary>
        /// <param name="outcome">The non-null semantic run outcome to classify.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="outcome"/> represents successful output or idle completion.</exception>
        public static void ThrowIfSuccessfulRunOutcome(
            AgentRunOutcome outcome,
            [CallerArgumentExpression(nameof(outcome))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(outcome, paramName);
            if (outcome is AgentRunCompleted or AgentRunIdle or RunSucceeded or RunIdle)
            {
                throw new ArgumentException("A halt proposal cannot carry a successful outcome.", paramName);
            }
        }

        /// <summary>Throws when committed tool references do not correlate exactly with a complete assistant response.</summary>
        /// <param name="response">The non-null committed assistant response.</param>
        /// <param name="toolResults">The initialized references to validate.</param>
        /// <param name="paramName">The parameter name inferred from the reference-array expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
        /// <exception cref="ArgumentException">The response or reference correlation is incomplete, duplicate, or inconsistent.</exception>
        public static void ThrowIfInvalidCommittedToolReferences(
            AssistantMessage response,
            ImmutableArray<CommittedToolResultReference> toolResults,
            [CallerArgumentExpression(nameof(toolResults))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentException.ThrowIfContainsNull(toolResults, paramName);
            if (response.State != MessageState.Complete
                || response.RunId is not { } runId
                || runId == default
                || response.TurnId is not { } turnId
                || turnId == default)
            {
                throw new ArgumentException("Committed-turn evidence requires a complete response with run and turn identity.", paramName);
            }

            var callIds = response.Parts.OfType<ToolCallPart>().Select(static part => part.CallId).ToImmutableArray();
            if (callIds.Any(static callId => callId == default)
                || callIds.Distinct().Count() != callIds.Length
                || !toolResults.Select(static reference => reference.ToolCallId).SequenceEqual(callIds)
                || toolResults.Any(reference => reference.TurnId != turnId)
                || toolResults.Select(static reference => reference.ToolCallId).Distinct().Count() != toolResults.Length
                || toolResults.Select(static reference => reference.SessionEntryId).Distinct().Count() != toolResults.Length)
            {
                throw new ArgumentException("Committed tool references must uniquely match calls and the turn in the assistant response.", paramName);
            }
        }

        /// <summary>Throws when committed tool-result references repeat a call or terminal session entry.</summary>
        /// <param name="toolResults">The initialized non-null references to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="toolResults"/> is default, contains null, or contains duplicate call or entry identities.</exception>
        public static void ThrowIfDuplicateCommittedToolReferences(
            ImmutableArray<CommittedToolResultReference> toolResults,
            [CallerArgumentExpression(nameof(toolResults))] string? paramName = null)
        {
            ArgumentException.ThrowIfContainsNull(toolResults, paramName);
            if (toolResults.Select(static reference => reference.ToolCallId).Distinct().Count() != toolResults.Length
                || toolResults.Select(static reference => reference.SessionEntryId).Distinct().Count() != toolResults.Length)
            {
                throw new ArgumentException("Committed tool references must have unique call and terminal-entry identities.", paramName);
            }
        }

        /// <summary>Throws when a claimed successful compaction no longer carries active checkpoint evidence.</summary>
        /// <param name="compaction">The non-null successful result to inspect.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="compaction"/> is null.</exception>
        /// <exception cref="ArgumentException">The retained evidence has a default identity, contexts disagree, or the record is not an active checkpoint.</exception>
        public static void ThrowIfCompactionNotActive(
            CompactionSucceeded compaction,
            [CallerArgumentExpression(nameof(compaction))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(compaction, paramName);
            if (compaction.Record is null
                || compaction.Context is null
                || compaction.Record.Context is null
                || compaction.Record.Manifest is null
                || compaction.Record.Manifest.Context is null
                || compaction.Context.CompactionId == default
                || compaction.Context.AgentId == default
                || compaction.Context.SessionId == default
                || compaction.Context.Correlation is null
                || compaction.Context.Identity is null
                || compaction.Record.Manifest.Id == default
                || compaction.Record.Manifest.BranchId == default
                || compaction.Record.Context != compaction.Context
                || compaction.Record.Manifest.Context != compaction.Context
                || compaction.Record.Status != CompactionRecordStatus.Active
                || compaction.Record.Checkpoint is null
                || compaction.Record.ActivatedSessionVersion is null)
            {
                throw new ArgumentException("Compaction continuation requires an active checkpoint record.", paramName);
            }
        }

        /// <summary>Throws when pending causes repeat the cause already selected by policy.</summary>
        /// <param name="selectedCause">The non-null selected cause.</param>
        /// <param name="otherPendingCauses">The initialized non-null pending causes.</param>
        /// <param name="paramName">The parameter name inferred from the pending-cause expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="selectedCause"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="otherPendingCauses"/> is default, contains null, or contains the selected instance.</exception>
        public static void ThrowIfContainsSelectedCause(
            RunContinuationCause selectedCause,
            ImmutableArray<RunContinuationCause> otherPendingCauses,
            [CallerArgumentExpression(nameof(otherPendingCauses))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(selectedCause);
            ArgumentException.ThrowIfContainsNull(otherPendingCauses, paramName);
            if (otherPendingCauses.Any(cause => ReferenceEquals(cause, selectedCause)))
            {
                throw new ArgumentException("Pending causes must not repeat the selected cause instance.", paramName);
            }
        }

        /// <summary>Throws when continuation boundary or cause evidence does not correlate with its owning run snapshot.</summary>
        /// <param name="agentId">The owning agent.</param>
        /// <param name="sessionId">The owning session.</param>
        /// <param name="executionLaneId">The owning lane.</param>
        /// <param name="operationId">The installed operation.</param>
        /// <param name="runId">The open run.</param>
        /// <param name="operationStateRevision">The captured operation revision.</param>
        /// <param name="branchCursor">The captured branch tip.</param>
        /// <param name="inputPromotionCutoff">The latest admission sequence included in the captured evaluation.</param>
        /// <param name="boundary">The safe evaluation boundary.</param>
        /// <param name="causes">The initialized pending causes.</param>
        /// <exception cref="ArgumentException">Any evidence is unrelated to the captured run, installed operation, revision, cursor, request, or turn.</exception>
        public static void ThrowIfInconsistentContinuationEvidence(
            AgentId agentId,
            SessionId sessionId,
            ExecutionLaneId executionLaneId,
            OperationId operationId,
            RunId runId,
            OperationStateRevision operationStateRevision,
            SessionBranchCursor branchCursor,
            SessionSequence inputPromotionCutoff,
            RunContinuationBoundary boundary,
            ImmutableArray<RunContinuationCause> causes)
        {
            ArgumentNullException.ThrowIfNull(branchCursor);
            ArgumentNullException.ThrowIfNull(boundary);
            ArgumentException.ThrowIfContainsNull(causes);
            if (causes.Distinct(ReferenceEqualityComparer.Instance).Count() != causes.Length)
            {
                throw new ArgumentException("Continuation causes must not repeat the same evidence instance.", nameof(causes));
            }

            if (boundary is CommittedTurnContinuationBoundary committed
                && (committed.Response.AgentId != agentId
                    || committed.Response.SessionId != sessionId
                    || committed.Response.BranchId != branchCursor.BranchId
                    || committed.Response.RunId != runId))
            {
                throw new ArgumentException("Committed response correlation must match the continuation snapshot.", nameof(boundary));
            }

            foreach (var cause in causes)
            {
                if (cause is PromotedInputContinuationCause promoted
                    && (promoted.Snapshot.AgentId != agentId
                        || promoted.Snapshot.SessionId != sessionId
                        || promoted.Snapshot.ExecutionLaneId != executionLaneId
                        || promoted.Snapshot.ExpectedOperation.OperationId != operationId
                        || promoted.Snapshot.ExpectedOperation.RunId != runId
                        || promoted.Snapshot.OperationStateRevision != operationStateRevision
                        || promoted.Snapshot.BranchCursor != branchCursor
                        || promoted.Snapshot.CutoffSequence != inputPromotionCutoff
                        || !(boundary switch
                        {
                            CommittedTurnContinuationBoundary committedBoundary =>
                                promoted.Snapshot.Boundary == PromotionBoundary.AfterTurnCommitted
                                && promoted.Snapshot.PreviousTurnId == committedBoundary.Response.TurnId,
                            RetryContinuationBoundary retryBoundary =>
                                promoted.Snapshot.Boundary == PromotionBoundary.AfterContinuationCheckpoint
                                && promoted.Snapshot.TargetTurnId == retryBoundary.TurnId,
                            DeferredContinuationBoundary promotedDeferredBoundary =>
                                promoted.Snapshot.Boundary == PromotionBoundary.AfterContinuationCheckpoint
                                && promoted.Snapshot.TargetTurnId == promotedDeferredBoundary.TurnId,
                            IdleContinuationBoundary => promoted.Snapshot.Boundary == PromotionBoundary.OtherwiseIdle,
                            _ => false,
                        })))
                {
                    throw new ArgumentException("Promoted input evidence must match the continuation snapshot.", nameof(causes));
                }

                if (cause is CommittedToolResultsContinuationCause tools
                    && (boundary is not CommittedTurnContinuationBoundary toolBoundary
                        || !tools.ToolResults.SequenceEqual(toolBoundary.ToolResults)))
                {
                    throw new ArgumentException("Committed tool continuation requires the same committed-turn references.", nameof(causes));
                }

                if (cause is OutputRepairContinuationCause repair
                    && (boundary is not CommittedTurnContinuationBoundary { OutputDecision: OutputRetryRequired decision }
                        || !repair.Decision.Equals(decision)))
                {
                    throw new ArgumentException("Output repair continuation requires the same committed-turn retry decision.", nameof(causes));
                }

                if (cause is CompactionRetryContinuationCause compaction
                    && (boundary is not RetryContinuationBoundary retry
                        || retry.ModelRequestId != compaction.ModelRequestId
                        || compaction.Compaction.Context.AgentId != agentId
                        || compaction.Compaction.Context.SessionId != sessionId
                        || compaction.Compaction.Context.Correlation is not InRunOperationCorrelation compactionCorrelation
                        || compactionCorrelation.RunId != runId
                        || compactionCorrelation.OperationId == default
                        || compactionCorrelation.TurnId != retry.TurnId
                        || compaction.Compaction.Record.Manifest.BranchId != branchCursor.BranchId))
                {
                    throw new ArgumentException("Compaction retry evidence must match the retry boundary and run.", nameof(causes));
                }

                if (cause is DeferredCompletionContinuationCause deferred
                    && (boundary is not DeferredContinuationBoundary deferredBoundary
                        || deferredBoundary.DeferredOperationId != deferred.OperationId))
                {
                    throw new ArgumentException("Deferred completion evidence must match the deferred boundary.", nameof(causes));
                }
            }
        }

        /// <summary>Throws when a type cannot name one closed service contract.</summary>
        /// <param name="type">The non-null type that must not contain unbound generic parameters.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="type"/> is an open generic type or otherwise contains unbound generic parameters.</exception>
        public static void ThrowIfNotClosedType(
            Type type,
            [CallerArgumentExpression(nameof(type))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(type, paramName);
            if (type.ContainsGenericParameters)
            {
                throw new ArgumentException("Type must be closed and cannot contain unbound generic parameters.", paramName);
            }
        }

        /// <summary>Throws when a type cannot construct a concrete closed implementation.</summary>
        /// <param name="type">The non-null implementation type that must be closed, non-abstract, and non-interface.</param>
        /// <param name="paramName">The parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="type"/> is open, abstract, or an interface.</exception>
        public static void ThrowIfNotConcreteClosedType(
            Type type,
            [CallerArgumentExpression(nameof(type))] string? paramName = null)
        {
            ArgumentException.ThrowIfNotClosedType(type, paramName);
            if (type.IsAbstract || type.IsInterface)
            {
                throw new ArgumentException("Type must be a concrete, non-interface implementation.", paramName);
            }
        }

        /// <summary>Throws when a type cannot be used as a closed reference-type component service contract.</summary>
        /// <param name="type">The non-null service contract type to validate.</param>
        /// <param name="paramName">The parameter name inferred from the type expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="type"/> is open, void, a pointer, by-reference, by-reference-like, or a value type.</exception>
        public static void ThrowIfNotComponentContractType(
            Type type,
            [CallerArgumentExpression(nameof(type))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(type, paramName);
            ArgumentException.ThrowIfNotClosedType(type, paramName);
            if (type == typeof(void)
                || type.IsPointer
                || type.IsFunctionPointer
                || type.IsByRef
                || type.IsByRefLike
                || type.IsValueType)
            {
                throw new ArgumentException("Component contract type must be a closed reference type usable by dependency injection.", paramName);
            }
        }

        /// <summary>Throws when a type cannot construct a closed reference-type component implementation.</summary>
        /// <param name="type">The non-null implementation type to validate.</param>
        /// <param name="paramName">The parameter name inferred from the type expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="type"/> is open, abstract, an interface, void, a pointer, by-reference, by-reference-like, or a value type.</exception>
        public static void ThrowIfNotComponentImplementationType(
            Type type,
            [CallerArgumentExpression(nameof(type))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(type, paramName);
            ArgumentException.ThrowIfNotConcreteClosedType(type, paramName);
            if (type == typeof(void)
                || type.IsPointer
                || type.IsFunctionPointer
                || type.IsByRef
                || type.IsByRefLike
                || type.IsValueType)
            {
                throw new ArgumentException("Component implementation type must be a concrete closed reference type usable by dependency injection.", paramName);
            }
        }

        /// <summary>Throws when original and effective admitted payload evidence cannot describe one idempotent input.</summary>
        /// <param name="originalPayload">The non-null original caller payload.</param>
        /// <param name="effectivePayload">The non-null captured effective payload.</param>
        /// <param name="preprocessing">The non-null preprocessing evidence.</param>
        /// <param name="paramName">The effective-payload parameter name attributed to a mismatch.</param>
        /// <exception cref="ArgumentNullException">A supplied reference is null.</exception>
        /// <exception cref="ArgumentException">Payload identities or delivery classes differ, or preprocessing fingerprints are default.</exception>
        public static void ThrowIfInvalidAdmittedInputPayloads(
            AgentInput originalPayload,
            AgentInput effectivePayload,
            InputPreprocessingManifest preprocessing,
            [CallerArgumentExpression(nameof(effectivePayload))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(originalPayload);
            ArgumentNullException.ThrowIfNull(effectivePayload, paramName);
            ArgumentNullException.ThrowIfNull(preprocessing);
            if (originalPayload.Id != effectivePayload.Id
                || originalPayload.Delivery != effectivePayload.Delivery
                || preprocessing.OriginalFingerprint == default
                || preprocessing.EffectiveFingerprint == default)
            {
                throw new ArgumentException("Original and effective payloads must preserve input identity and delivery with initialized preprocessing evidence.", paramName);
            }
        }

        /// <summary>Throws when a promotion selection is uninitialized or repeats an admission identity.</summary>
        /// <param name="admissionIds">The initialized ordered admission identities.</param>
        /// <param name="paramName">The selection parameter name.</param>
        /// <exception cref="ArgumentException">The array is default or contains a default or duplicate identity.</exception>
        public static void ThrowIfInvalidPromotionAdmissions(
            ImmutableArray<AdmissionId> admissionIds,
            [CallerArgumentExpression(nameof(admissionIds))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefaultOrEmpty(admissionIds, paramName);
            var seen = new HashSet<AdmissionId>();
            foreach (var admissionId in admissionIds)
            {
                if (admissionId == default || !seen.Add(admissionId))
                {
                    throw new ArgumentException("Promotion admission identities must be initialized and unique.", paramName);
                }
            }
        }

        /// <summary>Throws when eligible input is not a pending item for the captured address, lane, and cutoff.</summary>
        /// <param name="eligible">The initialized eligible snapshot.</param><param name="agentId">The expected agent.</param>
        /// <param name="sessionId">The expected session.</param><param name="executionLaneId">The expected lane.</param>
        /// <param name="cutoffSequence">The inclusive admission cutoff.</param><param name="paramName">The eligible parameter name.</param>
        /// <exception cref="ArgumentException">The snapshot is default, contains null, repeats admissions, or contains a mismatched, promoted, or post-cutoff item.</exception>
        public static void ThrowIfInvalidPromotionEligibleInputs(
            ImmutableArray<AdmittedInput> eligible,
            AgentId agentId,
            SessionId sessionId,
            ExecutionLaneId executionLaneId,
            SessionSequence cutoffSequence,
            [CallerArgumentExpression(nameof(eligible))] string? paramName = null)
        {
            ArgumentException.ThrowIfContainsNull(eligible, paramName);
            var seen = new HashSet<AdmissionId>();
            foreach (var input in eligible)
            {
                if (input.AgentId != agentId || input.SessionId != sessionId || input.ExecutionLaneId != executionLaneId
                    || input.AdmittedSequence.Value > cutoffSequence.Value || input.PromotedSequence is not null
                    || !seen.Add(input.AdmissionId))
                {
                    throw new ArgumentException("Eligible input must be unique, pending, and match the captured address, lane, and cutoff.", paramName);
                }
            }
        }

        /// <summary>Throws when authenticated identity differs from the identity captured by authorization evidence.</summary>
        /// <param name="identity">The non-null authenticated identity.</param><param name="authorization">The non-null authorization context.</param>
        /// <param name="paramName">The authorization parameter name.</param>
        /// <exception cref="ArgumentNullException">A supplied reference is null.</exception>
        /// <exception cref="ArgumentException">The immutable identities are not equal.</exception>
        public static void ThrowIfInputAuthorizationIdentityMismatch(
            ExecutionIdentity identity,
            SecurityAuthorizationContext authorization,
            [CallerArgumentExpression(nameof(authorization))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(authorization, paramName);
            if (identity != authorization.Identity)
            {
                throw new ArgumentException("Input identity must equal authorization identity evidence.", paramName);
            }
        }

        /// <summary>Throws when authorization does not exactly bind an operation's identity, agent, optional session, and causal correlation.</summary>
        /// <param name="identity">The complete authenticated caller identity.</param><param name="agentId">The addressed agent.</param>
        /// <param name="sessionId">The addressed session, or null for truthful sessionless work.</param><param name="correlation">The exact causal operation.</param>
        /// <param name="authorization">The captured authorization evidence.</param><param name="paramName">The authorization parameter name.</param>
        /// <exception cref="ArgumentNullException">A required reference is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> or a supplied <paramref name="sessionId"/> is default.</exception>
        /// <exception cref="ArgumentException">Identity, address, or correlation differs from authorization scope.</exception>
        public static void ThrowIfInvalidOperationAuthorization(
            ExecutionIdentity identity,
            AgentId agentId,
            SessionId? sessionId,
            OperationCorrelation correlation,
            SecurityAuthorizationContext authorization,
            [CallerArgumentExpression(nameof(authorization))] string? paramName = null)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
            if (sessionId is { } value)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(sessionId));
            }
            ArgumentException.ThrowIfInputAuthorizationIdentityMismatch(identity, authorization, paramName);
            ArgumentNullException.ThrowIfNull(correlation);
            if (authorization.Scope.AgentId != agentId
                || authorization.Scope.SessionId != sessionId
                || authorization.Scope.Correlation != correlation)
            {
                throw new ArgumentException(
                    "Authorization must exactly bind the operation identity, address, and causal correlation.", paramName);
            }
        }

        /// <summary>Throws when captured authorization does not bind the requested agent, session, identity, and active run.</summary>
        /// <param name="identity">The complete authenticated identity.</param><param name="agentId">The run's agent.</param>
        /// <param name="sessionId">The run's session.</param><param name="runId">The active run identity.</param>
        /// <param name="authorization">The captured run-start authorization.</param><param name="paramName">The authorization parameter name.</param>
        /// <exception cref="ArgumentNullException">A required reference is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A supplied domain identity is default.</exception>
        /// <exception cref="ArgumentException">Identity, address, or active-run evidence differs.</exception>
        public static void ThrowIfInvalidRunAuthorization(
            ExecutionIdentity identity,
            AgentId agentId,
            SessionId sessionId,
            RunId runId,
            SecurityAuthorizationContext authorization,
            [CallerArgumentExpression(nameof(authorization))] string? paramName = null)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
            ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default, nameof(sessionId));
            ArgumentOutOfRangeException.ThrowIfEqual(runId, default, nameof(runId));
            ArgumentException.ThrowIfInputAuthorizationIdentityMismatch(identity, authorization, paramName);
            if (authorization.Scope.AgentId != agentId
                || authorization.Scope.SessionId != sessionId
                || authorization.Scope.Correlation is not InRunOperationCorrelation correlation
                || correlation.RunId != runId)
            {
                throw new ArgumentException(
                    "Run authorization must bind the requested identity, agent, session, and active run.", paramName);
            }
        }

        /// <summary>Throws when admission authorization does not exactly bind caller identity, address, and causal operation.</summary>
        /// <param name="identity">The authenticated caller identity.</param><param name="agentId">The addressed agent.</param>
        /// <param name="sessionId">The addressed session.</param><param name="correlation">The admission correlation.</param>
        /// <param name="authorization">The captured authorization evidence.</param><param name="paramName">The authorization parameter name.</param>
        /// <exception cref="ArgumentNullException">A required reference is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> or <paramref name="sessionId"/> is default.</exception>
        /// <exception cref="ArgumentException">Identity, address, or correlation differs from authorization scope.</exception>
        public static void ThrowIfInvalidInputAdmissionAuthorization(
            ExecutionIdentity identity,
            AgentId agentId,
            SessionId sessionId,
            OperationCorrelation correlation,
            SecurityAuthorizationContext authorization,
            [CallerArgumentExpression(nameof(authorization))] string? paramName = null)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
            ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default, nameof(sessionId));
            ArgumentException.ThrowIfInputAuthorizationIdentityMismatch(identity, authorization, paramName);
            ArgumentNullException.ThrowIfNull(correlation);
            if (authorization.Scope.AgentId != agentId
                || authorization.Scope.SessionId != sessionId
                || authorization.Scope.Correlation != correlation)
            {
                throw new ArgumentException("Admission authorization must exactly bind the addressed agent, session, and causal operation.", paramName);
            }
        }

        /// <summary>Throws when promotion authorization does not bind caller identity, address, and active run.</summary>
        /// <param name="identity">The authenticated caller identity.</param><param name="agentId">The addressed agent.</param>
        /// <param name="sessionId">The addressed session.</param><param name="expectedOperation">The installed lane operation.</param>
        /// <param name="authorization">The distinct promotion-invocation authorization evidence.</param><param name="paramName">The authorization parameter name.</param>
        /// <exception cref="ArgumentNullException">A required reference is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> or <paramref name="sessionId"/> is default.</exception>
        /// <exception cref="ArgumentException">Identity, address, or active run differs from authorization scope.</exception>
        public static void ThrowIfInvalidInputPromotionAuthorization(
            ExecutionIdentity identity,
            AgentId agentId,
            SessionId sessionId,
            InRunOperationCorrelation expectedOperation,
            SecurityAuthorizationContext authorization,
            [CallerArgumentExpression(nameof(authorization))] string? paramName = null)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
            ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default, nameof(sessionId));
            ArgumentException.ThrowIfInputAuthorizationIdentityMismatch(identity, authorization, paramName);
            ArgumentNullException.ThrowIfNull(expectedOperation);
            if (authorization.Scope.AgentId != agentId
                || authorization.Scope.SessionId != sessionId
                || authorization.Scope.Correlation is not InRunOperationCorrelation invocation
                || invocation.RunId != expectedOperation.RunId)
            {
                throw new ArgumentException("Promotion authorization must bind the addressed agent, session, and active run.", paramName);
            }
        }

        /// <summary>Throws when committed promoted records do not exactly match snapshot order and address.</summary>
        /// <param name="snapshot">The non-null committed selection evidence.</param><param name="promoted">The initialized promoted records.</param>
        /// <param name="paramName">The promoted-record parameter name.</param>
        /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
        /// <exception cref="ArgumentException">Records are default, null, misordered, admitted after the snapshot cutoff, unpromoted, or addressed differently from the snapshot.</exception>
        public static void ThrowIfInvalidPromotedInputs(
            InputPromotionSnapshot snapshot,
            ImmutableArray<AdmittedInput> promoted,
            [CallerArgumentExpression(nameof(promoted))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentException.ThrowIfContainsNull(promoted, paramName);
            if (promoted.Length != snapshot.AdmissionIds.Length)
            {
                throw new ArgumentException("Promoted records must match the snapshot selection count.", paramName);
            }

            for (var index = 0; index < promoted.Length; index++)
            {
                var input = promoted[index];
                if (input.AdmissionId != snapshot.AdmissionIds[index]
                    || input.AgentId != snapshot.AgentId
                    || input.SessionId != snapshot.SessionId
                    || input.ExecutionLaneId != snapshot.ExecutionLaneId
                    || input.AdmittedSequence.Value > snapshot.CutoffSequence.Value
                    || input.PromotedSequence is null)
                {
                    throw new ArgumentException("Promoted records must exactly match snapshot order, address, lane, and committed state.", paramName);
                }
            }
        }

        /// <summary>Throws when previous and target turns do not match the named promotion boundary.</summary>
        /// <param name="boundary">The validated safe boundary.</param><param name="previousTurnId">The prior committed turn, when applicable.</param>
        /// <param name="targetTurnId">The nondefault receiving turn.</param><param name="paramName">The previous-turn parameter name.</param>
        /// <exception cref="ArgumentOutOfRangeException">The boundary is undefined, the target turn is default, or a present previous turn is default.</exception>
        /// <exception cref="ArgumentException">A committed-turn boundary lacks a distinct previous turn, or a first-request boundary supplies one.</exception>
        public static void ThrowIfInvalidPromotionTurnBoundary(
            PromotionBoundary boundary,
            TurnId? previousTurnId,
            TurnId targetTurnId,
            [CallerArgumentExpression(nameof(previousTurnId))] string? paramName = null)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(boundary);
            if (previousTurnId is { } previous)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(previous, default, nameof(previousTurnId));
            }
            ArgumentOutOfRangeException.ThrowIfEqual(targetTurnId, default, nameof(targetTurnId));
            if ((boundary == PromotionBoundary.AfterTurnCommitted
                    && (previousTurnId is null || previousTurnId == targetTurnId))
                || (boundary == PromotionBoundary.BeforeFirstModelRequest && previousTurnId is not null))
            {
                throw new ArgumentException("Promotion previous and target turns must match the selected safe-boundary semantics.", paramName);
            }
        }

        /// <summary>Throws when a concrete implementation cannot satisfy its declared service or disposal contract.</summary>
        /// <param name="implementationType">The non-null closed implementation type to test.</param>
        /// <param name="contractType">The non-null closed contract the implementation must satisfy.</param>
        /// <param name="paramName">The parameter name inferred from the implementation expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="implementationType"/> or <paramref name="contractType"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="implementationType"/> is not assignable to <paramref name="contractType"/>.</exception>
        public static void ThrowIfNotAssignableTo(
            Type implementationType,
            Type contractType,
            [CallerArgumentExpression(nameof(implementationType))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(implementationType, paramName);
            ArgumentNullException.ThrowIfNull(contractType);
            if (!contractType.IsAssignableFrom(implementationType))
            {
                throw new ArgumentException("Implementation type must be assignable to the declared contract.", paramName);
            }
        }

        /// <summary>Throws when a factory boundary claims a contract that cannot dispose an owned operation.</summary>
        /// <param name="contractType">The non-null closed contract used to dispose the operation root.</param>
        /// <param name="paramName">The parameter name inferred from the contract expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="contractType"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="contractType"/> is open or is neither <see cref="IDisposable"/> nor <see cref="IAsyncDisposable"/>.</exception>
        public static void ThrowIfNotDisposalContract(
            Type contractType,
            [CallerArgumentExpression(nameof(contractType))] string? paramName = null)
        {
            ArgumentException.ThrowIfNotClosedType(contractType, paramName);
            if (contractType != typeof(IDisposable) && contractType != typeof(IAsyncDisposable))
            {
                throw new ArgumentException("Contract must be IDisposable or IAsyncDisposable.", paramName);
            }
        }

        /// <summary>Throws when a directory write location does not exactly bind the requesting session context.</summary>
        /// <param name="context">The non-null operation context expected to own the location.</param>
        /// <param name="location">The non-null candidate location whose address and tenant must match <paramref name="context"/>.</param>
        /// <param name="paramName">The location parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="location"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="location"/> has a different address or tenant from <paramref name="context"/>.</exception>
        public static void ThrowIfInvalidSessionDirectoryWriteBinding(
            SessionOperationContext context,
            SessionLocation location,
            [CallerArgumentExpression(nameof(location))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(location);
            if (context.ToAddress() != location.Address || context.Identity.TenantId != location.TenantId)
            {
                throw new ArgumentException("The location address and tenant must match the operation context.", paramName);
            }
        }

        /// <summary>Throws when a creation-route location does not exactly bind the canonical outer creation request.</summary>
        /// <param name="request">The non-null sessionless outer creation request expected to own the route.</param>
        /// <param name="location">The non-null candidate location whose agent and tenant must match <paramref name="request"/>.</param>
        /// <param name="paramName">The location parameter name inferred from the call-site expression when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="location"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="location"/> has a different agent or tenant from <paramref name="request"/>.</exception>
        public static void ThrowIfInvalidSessionDirectoryCreationBinding(
            SessionCreateRequest request,
            SessionLocation location,
            [CallerArgumentExpression(nameof(location))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(location);
            if (location.Address.AgentId != request.AgentId || location.TenantId != request.Identity.TenantId)
            {
                throw new ArgumentException("The candidate location must match the creation request's agent and tenant.", paramName);
            }
        }

        /// <summary>Throws when copied reservation evidence cannot safely enter a ledger receipt.</summary>
        /// <param name="request">The reservation request to validate.</param>
        /// <param name="paramName">The parameter name attributed to invalid copied evidence.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="request"/> is null or has default dimension,
        /// unit, or idempotency-key evidence with null text.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="request"/> contains blank dimension, unit, or idempotency text.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="request"/> contains default identities or a nonpositive amount.</exception>
        public static void ThrowIfInvalidBudgetLedgerReservationRequest(
            BudgetReservationRequest request,
            [CallerArgumentExpression(nameof(request))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(request, paramName);
            ArgumentOutOfRangeException.ThrowIfEqual(request.ScopeId, default, paramName);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Dimension.Value, paramName);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Amount, paramName);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Unit.Value, paramName);
            ArgumentOutOfRangeException.ThrowIfEqual(request.OperationId, default, paramName);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey.Value, paramName);
        }

        /// <summary>Throws when accepted ledger receipts are mixed, duplicated, or invalid as one batch result.</summary>
        /// <param name="receipts">The initialized nonempty receipt sequence.</param>
        /// <param name="paramName">The parameter name attributed to invalid batch evidence.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="receipts"/> is default, empty, null-containing,
        /// mixed-scope, mixed-operation, repeats an identity or item key, or
        /// expresses one dimension in incompatible units.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// A receipt's copied original request carries a default identity or
        /// nonpositive amount.
        /// </exception>
        public static void ThrowIfInvalidBudgetLedgerBatchReceipts(
            ImmutableArray<BudgetLedgerReservationReceipt> receipts,
            [CallerArgumentExpression(nameof(receipts))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefaultOrEmpty(receipts, paramName);
            ArgumentException.ThrowIfContainsNull(receipts, paramName);
            var scope = receipts[0].Reservation.Scope;
            var reservations = new HashSet<BudgetReservationId>();
            var itemKeys = new HashSet<IdempotencyKey>();
            foreach (var receipt in receipts)
            {
                ArgumentException.ThrowIfNotEqual(receipt.Reservation.Scope, scope, paramName);
                ArgumentException.ThrowIfInvalidBudgetLedgerReservationRequest(receipt.OriginalRequest, paramName);
                if (!reservations.Add(receipt.Reservation.Id) || !itemKeys.Add(receipt.OriginalRequest.IdempotencyKey))
                {
                    throw new ArgumentException("Ledger batch receipts must have unique reservation identities and item keys.", paramName);
                }
            }

            ArgumentException.ThrowIfInvalidBudgetReservationBatch(
                [.. receipts.Select(static receipt => receipt.OriginalRequest)],
                scope.Id,
                paramName);
        }

        /// <summary>Throws when a recovery page fails its scope, ordering, or continuation invariants.</summary>
        /// <param name="scope">The exact queried scope.</param>
        /// <param name="watermark">The scan revision returned by the ledger.</param>
        /// <param name="items">The initialized unresolved rows.</param>
        /// <param name="next">The optional continuation cursor.</param>
        /// <param name="paramName">The parameter name attributed to invalid page evidence.</param>
        /// <exception cref="ArgumentException"><paramref name="items"/> or <paramref name="next"/> is inconsistent with the scope, watermark, or canonical order.</exception>
        public static void ThrowIfInvalidBudgetUnresolvedReservationPage(
            BudgetLedgerScopeReference scope,
            BudgetLedgerWatermark watermark,
            ImmutableArray<BudgetUnresolvedReservation> items,
            BudgetReservationCursor? next,
            [CallerArgumentExpression(nameof(items))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(scope, nameof(scope));
            ArgumentOutOfRangeException.ThrowIfEqual(watermark, default, nameof(watermark));
            ArgumentException.ThrowIfDefault(items, paramName);
            ArgumentException.ThrowIfContainsNull(items, paramName);
            BudgetReservationId? previous = null;
            foreach (var item in items)
            {
                ArgumentException.ThrowIfNotEqual(item.Receipt.Reservation.Scope, scope, paramName);
                if (previous is { } prior && string.CompareOrdinal(prior.ToString(), item.Receipt.Reservation.Id.ToString()) >= 0)
                {
                    throw new ArgumentException("Unresolved reservations must be strictly ordered by canonical reservation identity.", paramName);
                }
                previous = item.Receipt.Reservation.Id;
            }
            if (next is not null && (next.Scope != scope || next.Watermark != watermark || previous is null || next.AfterReservationId != previous.Value))
            {
                throw new ArgumentException("The continuation cursor must exactly continue this page.", paramName);
            }
        }

        /// <summary>Throws when an overrun-resolution refusal contains no current accounting fact that blocks clearance.</summary>
        /// <param name="currentOverruns">The initialized current row-overrun evidence.</param>
        /// <param name="hardLimitFailures">The initialized current hard-ceiling evidence.</param>
        /// <param name="paramName">The parameter name attributed to the missing evidence.</param>
        /// <exception cref="ArgumentException">Both evidence arrays are empty.</exception>
        public static void ThrowIfNoBudgetOverrunResolutionBlockers(
            ImmutableArray<BudgetOverrunHold> currentOverruns,
            ImmutableArray<BudgetLimitFailure> hardLimitFailures,
            [CallerArgumentExpression(nameof(currentOverruns))] string? paramName = null)
        {
            ArgumentException.ThrowIfDefault(currentOverruns);
            ArgumentException.ThrowIfDefault(hardLimitFailures);
            if (currentOverruns.IsEmpty && hardLimitFailures.IsEmpty)
            {
                throw new ArgumentException("At least one blocking accounting fact is required.", paramName);
            }
        }

        /// <summary>Throws when a proposed budget boundary address cannot be an ancestor of a charged reservation address.</summary>
        /// <param name="boundary">The non-null proposed ancestor address.</param><param name="charged">The non-null charged scope address.</param><param name="paramName">The parameter name attributed to incoherent charged evidence.</param>
        /// <exception cref="ArgumentNullException">An address is null.</exception><exception cref="ArgumentException">Tenant, principal, or agent differs, or a boundary session, run, or operation identity differs from the charged address.</exception>
        public static void ThrowIfNotBudgetScopeAncestorAddress(
            BudgetScopeAddress boundary,
            BudgetScopeAddress charged,
            [CallerArgumentExpression(nameof(charged))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(boundary);
            ArgumentNullException.ThrowIfNull(charged);
            if (boundary.TenantId != charged.TenantId
                || boundary.PrincipalId != charged.PrincipalId
                || boundary.AgentId != charged.AgentId
                || (boundary.SessionId is not null && boundary.SessionId != charged.SessionId)
                || (boundary.RunId is not null && boundary.RunId != charged.RunId)
                || (boundary.OperationId is not null && boundary.OperationId != charged.OperationId))
            {
                throw new ArgumentException("The boundary address must be an ancestor of the charged reservation address.", paramName);
            }
        }
    }
}
