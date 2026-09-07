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
            OutputSchemaDialectId defaultDialect,
            ImmutableArray<OutputSchemaDialectId> supportedDialects,
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
            var dialects = new HashSet<OutputSchemaDialectId>();
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
            OutputSchemaDialectId dialect,
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
    }
}
