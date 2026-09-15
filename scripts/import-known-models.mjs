// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

// Regenerates src/AgentKit.Providers/Resources/known-models.json from the public OpenClaw model
// catalog (https://github.com/openclaw/catalog), which is itself assembled from models.dev and
// OpenClaw's plugin manifests. Only providers with a first-party AgentKit adapter are imported,
// under AgentKit's own provider identifiers. Run with `npm run models:import`.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const SOURCE_NAME = "openclaw/catalog";
const SOURCE_URL = "https://catalog.openclaw.ai/models/v1/catalog.json";
const OUTPUT = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "../src/AgentKit.Providers/Resources/known-models.json",
);

// OpenClaw provider id -> AgentKit ProviderId (see each package's *ProviderDefaults.ProviderId).
const PROVIDERS = {
  anthropic: "anthropic",
  openai: "openai",
  google: "google-gemini",
  deepseek: "deepseek",
  groq: "groq",
  mistral: "mistral-ai",
  moonshot: "moonshot-kimi",
  xai: "xai",
  zai: "z-ai",
  cohere: "cohere",
};

const STATUSES = new Set(["available", "preview", "deprecated", "disabled"]);

function money(value) {
  if (typeof value !== "number" || !Number.isFinite(value) || value < 0) {
    return undefined;
  }
  // Upstream values carry binary-float noise such as 0.09999999999999999.
  return Math.round(value * 1_000_000) / 1_000_000;
}

function positiveInteger(value) {
  return Number.isInteger(value) && value > 0 ? value : undefined;
}

function convert(openclawProviderId, model) {
  const providerId = PROVIDERS[openclawProviderId];
  const status = STATUSES.has(model.status) ? model.status : "available";
  const inputs = Array.isArray(model.input) ? model.input : ["text"];
  const cost = model.cost && typeof model.cost === "object" ? model.cost : undefined;
  const entry = {
    providerId,
    modelId: model.id,
    displayName: typeof model.name === "string" && model.name.trim() ? model.name.trim() : model.id,
    status,
    supportsReasoning: model.reasoning === true,
    supportsVisionInput: inputs.includes("image"),
    // The upstream publisher imports only rows with tool calling and text output.
    supportsToolCalls: true,
    maxContextTokens: positiveInteger(model.contextWindow),
    maxOutputTokens: positiveInteger(model.maxTokens),
  };
  if (Array.isArray(model.replaces) && model.replaces.length > 0) {
    entry.replaces = model.replaces.filter((value) => typeof value === "string");
  }
  if (typeof model.replacedBy === "string" && model.replacedBy.trim()) {
    entry.replacedBy = model.replacedBy.trim();
  }
  if (cost && (cost.input !== undefined || cost.output !== undefined)) {
    entry.pricing = {
      currency: "USD",
      inputPerMillionTokens: money(cost.input),
      outputPerMillionTokens: money(cost.output),
      cacheReadPerMillionTokens: money(cost.cacheRead),
      cacheWritePerMillionTokens: money(cost.cacheWrite),
    };
    for (const key of Object.keys(entry.pricing)) {
      if (entry.pricing[key] === undefined) {
        delete entry.pricing[key];
      }
    }
  }
  for (const key of Object.keys(entry)) {
    if (entry[key] === undefined) {
      delete entry[key];
    }
  }
  return entry;
}

async function main() {
  const response = await fetch(SOURCE_URL, { signal: AbortSignal.timeout(60_000) });
  if (!response.ok) {
    throw new Error(`${SOURCE_URL} responded ${response.status}`);
  }
  const bundle = await response.json();
  if (bundle?.schemaVersion !== 1 || typeof bundle.providers !== "object") {
    throw new Error("unexpected OpenClaw catalog shape");
  }

  const models = [];
  const seen = new Set();
  for (const [openclawProviderId, provider] of Object.entries(bundle.providers)) {
    if (!(openclawProviderId in PROVIDERS)) {
      continue;
    }
    for (const model of provider.models ?? []) {
      if (typeof model?.id !== "string" || !model.id.trim()) {
        continue;
      }
      const entry = convert(openclawProviderId, model);
      const key = `${entry.providerId}\u0000${entry.modelId}`;
      if (seen.has(key)) {
        throw new Error(`duplicate model ${entry.providerId}/${entry.modelId}`);
      }
      seen.add(key);
      models.push(entry);
    }
  }
  models.sort((a, b) => a.providerId.localeCompare(b.providerId) || a.modelId.localeCompare(b.modelId));

  const output = {
    schemaVersion: 1,
    source: {
      name: SOURCE_NAME,
      url: SOURCE_URL,
      commit: typeof bundle.sourceCommit === "string" ? bundle.sourceCommit : null,
      generatedAt: new Date(bundle.generatedAt).toISOString(),
      importedAt: new Date().toISOString(),
    },
    providerIds: [...new Set(Object.values(PROVIDERS))].sort(),
    models,
  };
  fs.writeFileSync(OUTPUT, `${JSON.stringify(output, null, 2)}\n`);
  const byProvider = models.reduce((acc, m) => ((acc[m.providerId] = (acc[m.providerId] ?? 0) + 1), acc), {});
  console.log(`wrote ${models.length} models to ${path.relative(process.cwd(), OUTPUT)}`);
  console.log(JSON.stringify(byProvider));
}

await main();
