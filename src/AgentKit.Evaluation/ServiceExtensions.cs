// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Registers the evaluation runner, evaluators, result stores, and report exporters.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the singular first-party evaluation runner and its replaceable defaults.</summary>
        /// <param name="configure">Optional runner caps; repeated calls compose their callbacks in call order.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="InvalidOperationException">More than one <see cref="IEvaluationRunner"/> is already registered.</exception>
        /// <remarks>
        /// <para>
        /// The runner is a thread-safe singleton bound to the one <see cref="AgentEngine"/> registered in the same service
        /// provider; a runner cannot accept an engine later or pair it with another catalog. It is registered with
        /// <c>TryAdd</c>, so an earlier <see cref="ReplaceEvaluationRunner{TRunner}"/> is preserved. The options are validated
        /// and copied into one immutable snapshot the first time the runner is resolved; the singleton never rereads them.
        /// </para>
        /// <para>No result store, evaluator, or exporter is installed by default; register each explicitly. Repeating this call is idempotent apart from composing further <paramref name="configure"/> callbacks.</para>
        /// </remarks>
        public IServiceCollection AddAgentEvaluation(Action<EvaluationOptions>? configure = null) =>
            EvaluationRegistration.AddDefault(services, configure);

        /// <summary>Replaces the evaluation runner with a custom implementation.</summary>
        /// <typeparam name="TRunner">The runner implementation, registered as a singleton.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Replacement removes every earlier runner registration, so exactly one runner remains.</remarks>
        public IServiceCollection ReplaceEvaluationRunner<TRunner>()
            where TRunner : class, IEvaluationRunner =>
            EvaluationRegistration.ReplaceRunner<TRunner>(services);

        /// <summary>Adds one evaluator under its stable key.</summary>
        /// <typeparam name="TEvaluator">The evaluator implementation, registered as a singleton.</typeparam>
        /// <param name="key">The stable key, which must equal the evaluator descriptor key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <exception cref="InvalidOperationException">The key is already registered for a different implementation.</exception>
        /// <remarks>Evaluators are additive. An identical repeat is idempotent; reusing a key for another implementation fails here instead of choosing the last registration.</remarks>
        public IServiceCollection AddEvaluator<TEvaluator>(EvaluatorKey key)
            where TEvaluator : class, IEvaluator =>
            EvaluationRegistration.AddEvaluator<TEvaluator>(services, key);

        /// <summary>Replaces the evaluator registered under a key, leaving every other evaluator untouched.</summary>
        /// <typeparam name="TEvaluator">The replacement implementation, registered as a singleton.</typeparam>
        /// <param name="key">The stable key, which must equal the replacement descriptor key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        public IServiceCollection ReplaceEvaluator<TEvaluator>(EvaluatorKey key)
            where TEvaluator : class, IEvaluator =>
            EvaluationRegistration.ReplaceEvaluator<TEvaluator>(services, key);

        /// <summary>Adds one result store under the key a plan selects it by.</summary>
        /// <typeparam name="TStore">The store implementation, registered as a singleton.</typeparam>
        /// <param name="key">The store key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <exception cref="InvalidOperationException">The key is already registered for a different implementation.</exception>
        /// <remarks>There is no hidden durable store: a plan persists results only through a store it names explicitly.</remarks>
        public IServiceCollection AddEvaluationResultStore<TStore>(EvaluationResultStoreKey key)
            where TStore : class, IEvaluationResultStore =>
            EvaluationRegistration.AddStore<TStore>(services, key);

        /// <summary>Adds one report exporter under the key a plan names it by.</summary>
        /// <typeparam name="TExporter">The exporter implementation, registered as a singleton.</typeparam>
        /// <param name="key">The exporter key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <exception cref="InvalidOperationException">The key is already registered for a different implementation.</exception>
        public IServiceCollection AddEvaluationReportExporter<TExporter>(EvaluationReportExporterKey key)
            where TExporter : class, IEvaluationReportExporter =>
            EvaluationRegistration.AddExporter<TExporter>(services, key);

        /// <summary>Registers the model judge evaluator under its documented key with an explicit judge model and bounded budget.</summary>
        /// <param name="configure">The judge configuration; <see cref="ModelJudgeOptions.JudgeModel"/> must be set.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException">No judge model alias is configured.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is out of range.</exception>
        /// <exception cref="InvalidOperationException">The judge key is already registered with a different configuration or evaluator.</exception>
        /// <remarks>The evaluator requires an <see cref="IModelJudgeClient"/>; register <see cref="AddModelRequestJudgeClient"/> or a replacement. Repeating an identical registration is idempotent.</remarks>
        public IServiceCollection AddModelJudgeEvaluator(Action<ModelJudgeOptions> configure) =>
            EvaluationRegistration.AddModelJudge(services, configure);

        /// <summary>Registers the first-party judge client that reaches the judge model through the provider-neutral catalog, selector, and adapter.</summary>
        /// <param name="configure">Optional client options.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>The client is registered with <c>TryAdd</c>, so an earlier custom <see cref="IModelJudgeClient"/> is preserved. It requires <see cref="IModelCatalog"/>, <see cref="IModelSelector"/>, and <see cref="ILlmModelResolver"/> in the same composition.</remarks>
        public IServiceCollection AddModelRequestJudgeClient(Action<ModelRequestJudgeClientOptions>? configure = null) =>
            EvaluationRegistration.AddModelRequestJudgeClient(services, configure);
    }
}
