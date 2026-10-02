// Compiles every ```csharp block in the repository's Markdown against the Release build output.
//
// How it works:
//   * Each Markdown file becomes one compilation unit in namespace `Docs.<file>`; blocks are split
//     into type declarations, members, body-less signatures (compiled as `extern`), and statement
//     chunks (compiled as `__Harness.Fragment_N` methods).
//   * Samples omit setup, so undeclared simple names are stubbed (typed in `knownStubs`, otherwise
//     `dynamic`), undeclared types are borrowed from another document that declares them or
//     stubbed, and ambiguous per-leaf type names are aliased from the surrounding context.
//   * Contract-shape blocks (a file-scoped `namespace X;`) are normative design text that mixes
//     accessibility and omits bodies; they are not compiled.
//   * `<!-- doc-sample: skip - reason -->` directly before a fence exempts one block whose code is
//     compiled elsewhere (an example application's internals) or is pseudocode. The reason is
//     mandatory; the number of exemptions is reported.
//
// Prerequisite: `dotnet build AgentKit.slnx --configuration Release` (see `make docs-samples`).

import { execFile } from "node:child_process";
import { mkdtemp, readdir, readFile, rm, writeFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, dirname, join, relative, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { promisify } from "node:util";

const execFileAsync = promisify(execFile);

// Directories that never hold authored documentation. ".worktrees" holds full checkouts of this
// repository at other commits; walking into one would validate stale docs against today's API.
const ignoredDirectories = new Set([
  ".claude",
  ".git",
  ".worktrees",
  "artifacts",
  "bin",
  "node_modules",
  "obj",
]);

const csharpFence = /^\s{0,3}```csharp\s*$/u;
const closingFence = /^\s{0,3}```\s*$/u;
// A block preceded by this comment is excerpted from code another build step already compiles
// (for example an example application's internal types) and is deliberately not compiled here.
// The reason after the dash is mandatory so every exemption stays reviewable.
const skipDirective = /^<!--\s*doc-sample:\s*skip\s+-\s+(\S.*?)\s*-->$/u;
const usingLine = /^\s*using\s+[\w.]+\s*;\s*$/u;
const typeDeclarationKeyword = /\b(?:class|struct|interface|enum|record)\b/u;
const memberModifierStart =
  /^(?:public\b|internal\b|private\b|protected\b|static\b|sealed\b|abstract\b|virtual\b|override\b|async\b|partial\b)/u;
// Test-framework attributes the samples use without referencing the test framework.
const attributeStubTypeNames = new Set(["Fact", "Theory", "InlineData"]);
const maxStubIterations = 16;
const maxConcurrentCompilations = 4;

// Unresolved names fall back to a `dynamic` field, which compiles for plain member access and
// invocation. C# forbids lambdas as arguments of dynamically dispatched calls (CS1977), so a name
// that is passed to such a call needs a typed stub. List those names here; every other undeclared
// name is stubbed as `dynamic`.
const knownStubs = new Map([
  ["cancellationToken", "internal global::System.Threading.CancellationToken cancellationToken = default;"],
  ["ct", "internal global::System.Threading.CancellationToken ct = default;"],
  ["apiKey", "internal string apiKey = \"\";"],
  ["prompt", "internal string prompt = \"\";"],
  ["services", "internal global::Microsoft.Extensions.DependencyInjection.IServiceCollection services = new global::Microsoft.Extensions.DependencyInjection.ServiceCollection();"],
  ["clock", "internal global::System.TimeProvider clock = global::System.TimeProvider.System;"],
  ["authenticatedAt", "internal global::System.DateTimeOffset authenticatedAt = default;"],
  ["expiresAt", "internal global::System.DateTimeOffset? expiresAt = default;"],
  ["user", "internal global::System.Security.Claims.ClaimsPrincipal user = new();"],
  ["authorityKey", "internal ComponentKey<ISecurityAuthority> authorityKey = default;"],
  ["alias", "internal ModelAlias alias = default;"],
  ["modelId", "internal ModelId modelId = default;"],
  ["definition", "internal AgentDefinition definition = default!;"],
  ["identity", "internal ExecutionIdentity identity = default!;"],
  ["securityPublication", "internal SecurityProfilePublication securityPublication = default!;"],
  ["securityProfilePublication", "internal SecurityProfilePublication securityProfilePublication = default!;"],
  ["runProfilePublication", "internal AgentRunProfilePublication runProfilePublication = default!;"],
  ["inputKey", "internal ComponentKey<IInputCoordinator> inputKey = default;"],
  ["outputKey", "internal ComponentKey<IOutputPublisher> outputKey = default;"],
  ["agentId", "internal AgentId agentId = default;"],
  ["ReviewerId", "internal AgentId ReviewerId = default;"],
  ["configuredInstanceId", "internal global::System.Guid configuredInstanceId = default;"],
  ["myStableGuid", "internal global::System.Guid myStableGuid = default;"],
  ["storeId", "internal global::System.Guid storeId = default;"],
  ["builder", "internal AgentEngineBuilder builder = AgentEngine.CreateBuilder();"],
  ["myDescriptor", "internal CompactionStrategyDescriptor myDescriptor = default!;"],
  ["scriptedResolver", "internal INetworkNameResolver scriptedResolver = default!;"],
  ["scriptedTransport", "internal INetworkTransport scriptedTransport = default!;"],
  ["ServiceIdentity", "internal ExecutionIdentity ServiceIdentity = default!;"],
  ["GrantStoreInstanceId", "internal SqliteSecurityGrantStoreInstanceId GrantStoreInstanceId = default;"],
  ["DecisionStoreInstanceId", "internal SqliteSecurityDecisionStoreInstanceId DecisionStoreInstanceId = default;"],
  ["ApprovalStoreInstanceId", "internal SqliteApprovalStoreInstanceId ApprovalStoreInstanceId = default;"],
  ["SupportedAuditKinds", "internal global::System.Collections.Immutable.ImmutableArray<SecurityAuditEventKind> SupportedAuditKinds = default;"],
  ["AllAuditEventKinds", "internal global::System.Collections.Immutable.ImmutableArray<SecurityAuditEventKind> AllAuditEventKinds = default;"],
  ["app", "internal global::Microsoft.AspNetCore.Builder.WebApplication app = default!;"],
  ["args", "internal string[] args = [];"],
  ["customer", "internal ExecutionIdentity customer = default!;"],
  ["checkoutRoot", "internal string checkoutRoot = \"\";"],
  ["prNumber", "internal int prNumber = 0;"],
  ["changedFiles", "internal string[] changedFiles = [];"],
  ["mySink", "internal ISecurityAuditSink mySink = default!;"],
  ["supportedEventKinds", "internal global::System.Collections.Immutable.ImmutableArray<SecurityAuditEventKind> supportedEventKinds = default;"],
  ["transport", "internal global::ModelContextProtocol.Client.IClientTransport transport = default!;"],
  ["definitionSource", "internal IAgentDefinitionSource definitionSource = default!;"],
  ["sessionProfileKey", "internal SessionProfileKey sessionProfileKey = default;"],
  ["securityProfileKey", "internal SecurityProfileKey securityProfileKey = default;"],
]);


// System namespaces every sample may use. Kept small: a broad list risks CS0104 ambiguity
// between same-named types in different namespaces.
const systemUsings = [
  "System",
  "System.Collections.Generic",
  "System.Collections.Immutable",
  "System.IO",
  "System.Linq",
  "System.Text",
  "System.Text.Json",
  "System.Threading",
  "System.Threading.Tasks",
  "System.Net",
  "System.Net.Http",
  "System.Security.Claims",
  "Shouldly",
  "Xunit",
  "Microsoft.AspNetCore.Builder",
  "Microsoft.AspNetCore.Http",
  "Microsoft.Extensions.DependencyInjection",
  "Microsoft.Extensions.DependencyInjection.Extensions",
  "Microsoft.Extensions.Hosting",
  "Microsoft.Extensions.Logging",
];

// Namespaces whose types are intentionally excluded from the harness using list because they
// collide with another namespace's bare type names; samples that need them are expected to
// qualify them.
const excludedNamespaces = new Set([]);

/**
 * Extracts every fenced ```csharp block from Markdown content.
 * @param {string} content
 * @returns {{code: string, startLine: number}[]}
 */
export function extractCSharpBlocks(content) {
  const lines = content.split("\n");
  const blocks = [];
  let index = 0;

  while (index < lines.length) {
    if (!csharpFence.test(lines[index])) {
      index++;
      continue;
    }

    // 1-based line of the first code line (the line after the opening fence).
    const startLine = index + 2;
    const bodyLines = [];
    let previous = index - 1;

    while (previous >= 0 && lines[previous].trim() === "") {
      previous--;
    }

    const skipped = previous >= 0 && skipDirective.test(lines[previous].trim());
    index++;

    while (index < lines.length && !closingFence.test(lines[index])) {
      bodyLines.push(lines[index]);
      index++;
    }

    blocks.push({ code: bodyLines.join("\n"), startLine, skipped });
    index++;
  }

  return blocks;
}

/**
 * Strips `using X;` lines from a snippet; the harness supplies the using list.
 * @param {string} code
 * @returns {string}
 */
export function stripUsingLines(code) {
  return code
    .split("\n")
    .filter((line) => !usingLine.test(line))
    .join("\n");
}

const signatureOnlyUnit =
  /^[A-Za-z_][\w<>\[\],.?\s]*\s[A-Za-z_]\w*(?:<[^()]*>)?\s*\([^{}]*\)\s*;$/u;
/**
 * Marks a body-less signature as `extern` so it compiles as a member of the harness class.
 * @param {string} code
 * @returns {string}
 */
function externalizeSignature(code) {
  if (/\b(?:abstract|extern|partial)\b/u.test(code)) {
    return code;
  }

  const lines = code.split("\n");
  const index = lines.findIndex((line) => line.trim().length > 0 && !/^\s*(?:\/\/|\[)/u.test(line));
  lines[index] = lines[index].replace(/^(\s*)/u, "$1extern ");
  return lines.join("\n");
}

const statementKeyword = /^(?:await|var|return|throw|new|yield|using|if|for|foreach|while)\b/u;

function stripLiteralsAndComments(line) {
  return line
    .replace(/"(?:[^"\\]|\\.)*"/gu, '""')
    .replace(/'(?:[^'\\]|\\.)'/gu, "''")
    .replace(/\/\/.*$/u, "");
}

/**
 * Classifies one top-level unit of a snippet by its first significant line.
 * @param {string[]} unitLines
 * @returns {"declaration" | "member" | "signature" | "statement"}
 */
function classifyUnit(unitLines) {
  const first = unitLines
    .map((line) => line.trim())
    .find((line) => line.length > 0 && !line.startsWith("//") && !line.startsWith("["));

  if (first === undefined) {
    return "statement";
  }

  if (typeDeclarationKeyword.test(first) && !statementKeyword.test(first) && !first.includes('"')) {
    return "declaration";
  }

  const unitText = unitLines
    .map((line) => line.trim())
    .filter((line) => !line.startsWith("//") && !line.startsWith("["))
    .join(" ");

  if (!statementKeyword.test(first) && !unitText.includes("=>") && signatureOnlyUnit.test(unitText)) {
    return "signature";
  }

  return memberModifierStart.test(first) ? "member" : "statement";
}

/**
 * Splits a using-stripped snippet into top-level units, each a type declaration, a member, a
 * body-less signature, or a statement run. A unit ends where brace depth returns to zero on a
 * line that ends with `;` or `}`. Raw string literals are skipped when counting braces.
 * @param {string} body
 * @returns {{kind: "declaration" | "member" | "signature" | "statement", code: string, offset: number}[]}
 */
export function splitBlock(body) {
  const lines = body.split("\n");
  const units = [];
  let current = [];
  let start = 0;
  let depth = 0;
  let inRawString = false;

  function flush() {
    if (current.length > 0) {
      units.push({ kind: classifyUnit(current), code: current.join("\n"), offset: start });
      current = [];
    }
  }

  for (let index = 0; index < lines.length; index++) {
    const line = lines[index];

    if (current.length === 0) {
      if (line.trim().length === 0) {
        continue;
      }

      start = index;
    }

    current.push(line);

    const tripleQuotes = (line.match(/"""/gu) ?? []).length;
    const touchesRawString = inRawString || tripleQuotes > 0;

    if (tripleQuotes % 2 === 1) {
      inRawString = !inRawString;
    }

    if (!touchesRawString) {
      const code = stripLiteralsAndComments(line);
      depth += (code.match(/\{/gu) ?? []).length - (code.match(/\}/gu) ?? []).length;
    }

    const trimmedCode = stripLiteralsAndComments(line).trim();
    const isCommentOnly = line.trim().startsWith("//");

    if (!inRawString && !isCommentOnly && depth <= 0 && /[;}]$/u.test(trimmedCode)) {
      flush();
      depth = 0;
    }
  }

  flush();
  return units;
}

/**
 * Classifies a using-stripped snippet body by the units it holds:
 * - "declaration": only type declarations; compiled at namespace scope.
 * - "member": only members or body-less signatures; compiled on the shared harness class.
 * - "program": statements, optionally mixed with declarations or members; the statements become
 *   an async method on the shared harness class and the declarations namespace-scope types.
 * - "fragment": no units at all (blank or comment-only).
 * @param {string} body
 * @returns {"declaration" | "program" | "member" | "fragment"}
 */
export function classifyBlock(body) {
  const kinds = new Set(splitBlock(body).map((unit) => unit.kind));

  if (kinds.size === 0) {
    return "fragment";
  }

  if (kinds.has("statement")) {
    return kinds.size === 1 ? "fragment" : "program";
  }

  return kinds.has("declaration") && kinds.size === 1 ? "declaration" : kinds.has("declaration") ? "program" : "member";
}

async function collectMarkdownFiles(root) {
  const results = [];

  async function walk(directory) {
    const entries = await readdir(directory, { withFileTypes: true });

    for (const entry of entries) {
      if (ignoredDirectories.has(entry.name)) {
        continue;
      }

      const path = join(directory, entry.name);

      if (entry.isDirectory()) {
        await walk(path);
      } else if (entry.isFile() && entry.name.endsWith(".md")) {
        results.push(path);
      }
    }
  }

  await walk(root);
  return results.sort();
}

function sanitizeIdentifier(value) {
  const sanitized = value.replace(/[^A-Za-z0-9_]/gu, "_");
  return /^[A-Za-z_]/u.test(sanitized) ? sanitized : `_${sanitized}`;
}

const fileScopedNamespace = /^namespace\s+([\w.]+)\s*;/mu;
const publicTypeDeclaration =
  /^\s*public\s+(?:(?:sealed|static|abstract|readonly|partial|ref|unsafe)\s+)*(?:class|struct|interface|enum|record)\b/mu;
const publicExtensionBlock = /^\s*public\s+static\s+class\b/mu;

const publicTypeName =
  /^\s*public\s+(?:(?:sealed|static|abstract|readonly|partial|ref|unsafe)\s+)*(?:class|struct|interface|enum|record(?:\s+struct|\s+class)?)\s+(\w+)/gmu;

/**
 * Scans the source tree for the `AgentKit.*` namespaces that declare at least one public type and
 * for the namespaces each public type name is declared in. Namespaces that only hold internal
 * types would not compile as `using` directives, and a type name declared in several namespaces
 * (the per-leaf SQLite option enums, for example) needs a per-sample alias.
 * @param {string} sourceRoot Directory holding one folder per project.
 * @returns {Promise<{namespaces: string[], typeNamespaces: Map<string, Set<string>>}>}
 */
export async function scanPublicTypes(sourceRoot) {
  const namespaces = new Set();
  const typeNamespaces = new Map();

  async function walk(directory) {
    const entries = await readdir(directory, { withFileTypes: true });

    for (const entry of entries) {
      if (ignoredDirectories.has(entry.name)) {
        continue;
      }

      const path = join(directory, entry.name);

      if (entry.isDirectory()) {
        await walk(path);
      } else if (entry.isFile() && entry.name.endsWith(".cs")) {
        const text = await readFile(path, "utf8");
        const namespaceMatch = fileScopedNamespace.exec(text);

        if (namespaceMatch === null) {
          continue;
        }

        if (publicTypeDeclaration.test(text) || publicExtensionBlock.test(text)) {
          namespaces.add(namespaceMatch[1]);
        }

        for (const match of text.matchAll(publicTypeName)) {
          if (match[1] === "ServiceExtensions") {
            // Every package's registration container; samples call the extension members, never the type.
            continue;
          }

          const known = typeNamespaces.get(match[1]) ?? new Set();
          known.add(namespaceMatch[1]);
          typeNamespaces.set(match[1], known);
        }
      }
    }
  }

  await walk(sourceRoot);
  return {
    namespaces: [...namespaces].filter((name) => !excludedNamespaces.has(name)).sort(),
    typeNamespaces,
  };
}

/**
 * Derives the `AgentKit.*` namespaces that declare at least one public type from the source tree.
 * @param {string} sourceRoot Directory holding one folder per project.
 * @returns {Promise<string[]>}
 */
export async function discoverPublicNamespaces(sourceRoot) {
  return (await scanPublicTypes(sourceRoot)).namespaces;
}

/**
 * Resolves per-sample `using Alias = Namespace.Type;` directives for type names declared in more
 * than one namespace. The intended namespace is the candidate that also declares the most other,
 * unambiguous type names the sample mentions.
 * @param {string} code All C# code of one Markdown file.
 * @param {Map<string, Set<string>>} typeNamespaces Public type name to declaring namespaces.
 * @returns {string[]} Alias directives, without the `using` keyword and semicolon.
 */
export function resolveAmbiguousTypeAliases(code, typeNamespaces) {
  const words = new Set(code.match(/\b[A-Za-z_]\w*\b/gu) ?? []);
  const aliases = [];

  for (const [name, declaring] of typeNamespaces) {
    if (declaring.size < 2 || !words.has(name)) {
      continue;
    }

    let best;
    let bestScore = 0;

    for (const candidate of [...declaring].sort()) {
      let score = 0;

      for (const word of words) {
        const owners = typeNamespaces.get(word);

        if (owners !== undefined && owners.size === 1 && owners.has(candidate)) {
          score++;
        }
      }

      if (score > bestScore) {
        best = candidate;
        bestScore = score;
      }
    }

    if (best !== undefined) {
      aliases.push(`${name} = ${best}.${name}`);
    }
  }

  return aliases;
}

/**
 * Reports whether a snippet is a normative contract-shape block, recognised by its file-scoped
 * `namespace X;` declaration.
 * @param {string} code
 * @returns {boolean}
 */
export function isContractShapeBlock(code) {
  return fileScopedNamespace.test(code);
}

/**
 * Renders one markdown file's blocks into a single compilable C# source.
 * @param {string} fileLabel
 * @param {{code: string, startLine: number}[]} blocks
 * @param {Set<string>} stubNames
 * @param {Set<string>} stubTypeNames
 * @param {string[]} usings Namespaces imported by the harness.
 * @param {string[]} borrowed Type declarations taken from other documents, emitted in the global namespace.
 * @returns {{source: string, lineMap: Map<number, number>}}
 */
export function renderCompilationUnit(fileLabel, blocks, stubNames, stubTypeNames, usings = systemUsings, borrowed = []) {
  const namespaceName = `Docs.${sanitizeIdentifier(fileLabel)}`;
  const declarationBlocks = [];
  const memberBlocks = [];
  const fragmentBlocks = [];

  for (const block of blocks) {
    const stripped = stripUsingLines(block.code);
    if (isContractShapeBlock(stripped)) {
      // Normative contract shapes declare a file-scoped namespace and spell out signatures the
      // way the architecture documents specify them (no bodies, mixed accessibility). They are
      // design text rather than samples, so they are not compiled.
      continue;
    }

    const statementChunks = [];

    for (const unit of splitBlock(stripped)) {
      const startLine = block.startLine + unit.offset;

      if (unit.kind === "declaration") {
        declarationBlocks.push({ code: unit.code, startLine });
      } else if (unit.kind === "member") {
        memberBlocks.push({ code: unit.code, startLine });
      } else if (unit.kind === "signature") {
        memberBlocks.push({ code: externalizeSignature(unit.code), startLine });
      } else {
        statementChunks.push({ code: unit.code, startLine });
      }
    }

    if (statementChunks.length > 0) {
      fragmentBlocks.push({ chunks: statementChunks });
    }
  }

  const outputLines = [];
  const lineMap = new Map();

  function emit(text, sourceStartLine) {
    const textLines = text.split("\n");

    for (let offset = 0; offset < textLines.length; offset++) {
      outputLines.push(textLines[offset]);

      if (sourceStartLine !== undefined) {
        lineMap.set(outputLines.length, sourceStartLine + offset);
      }
    }
  }

  emit("// Auto-generated by scripts/validate-doc-samples.mjs - DO NOT EDIT.");
  emit("#pragma warning disable");

  for (const namespaceUsing of usings) {
    emit(`using ${namespaceUsing};`);
  }

  emit("");
  emit(`namespace ${namespaceName}`);
  emit("{");
  emit(`    using static ${namespaceName}.__Stubs;`);
  emit("");
  emit("    internal static class __Stubs");
  emit("    {");

  for (const name of [...stubNames].sort()) {
    const known = knownStubs.get(name);
    emit(`        ${(known ?? `internal dynamic ${sanitizeIdentifier(name)} = default!;`).replace(/^internal /u, "internal static ")}`);
  }

  emit("    }");
  emit("");

  for (const declaration of declarationBlocks) {
    emit(declaration.code, declaration.startLine);
    emit("");
  }

  emit("    internal sealed class __Harness");
  emit("    {");

  for (const member of memberBlocks) {
    emit(member.code, member.startLine);
    emit("");
  }

  for (let index = 0; index < fragmentBlocks.length; index++) {
    const { chunks } = fragmentBlocks[index];
    const allCode = chunks.map((chunk) => chunk.code).join("\n");
    // A `return` inside a multi-line lambda body belongs to the lambda, not to the snippet.
    const hasBlockLambda = /=>\s*$/mu.test(allCode) || /=>\s*\{/u.test(allCode);
    const returnsValue = /^return\s+\S/mu.test(allCode) || (!hasBlockLambda && /\breturn\s+\S/u.test(allCode));
    const taskType = returnsValue
      ? "global::System.Threading.Tasks.Task<int>"
      : "global::System.Threading.Tasks.Task";
    const isChain = /^\s*\./u.test(chunks[0].code);

    emit(`        internal async ${taskType} Fragment_${index}()`);
    emit("        {");

    if (isChain) {
      // A snippet that continues a builder chain is evaluated against a fresh engine builder.
      emit("            _ = global::AgentKit.AgentEngine.CreateBuilder()");
    }

    for (const chunk of chunks) {
      emit(isChain ? `${chunk.code};` : chunk.code, chunk.startLine);
    }

    emit("        }");
    emit("");
  }

  emit("    }");
  emit("}");
  emit("");

  // Declarations borrowed from other documents and stubs for undeclared types live in the global
  // namespace so every sample namespace sees them.
  for (const declaration of borrowed) {
    emit(declaration);
    emit("");
  }

  for (const typeName of [...stubTypeNames].sort()) {
    const baseType = attributeStubTypeNames.has(typeName) ? " : global::System.Attribute" : "";
    emit(`public sealed class ${sanitizeIdentifier(typeName)}${baseType} { }`);
  }

  return { source: outputLines.join("\n"), lineMap };
}

function parseNames(compilerOutput, pattern) {
  const names = new Set();
  let match;

  while ((match = pattern.exec(compilerOutput)) !== null) {
    names.add(match[1]);
  }

  return names;
}

function parseUndeclaredNames(compilerOutput) {
  return parseNames(
    compilerOutput,
    /error CS0103: The name '([^']+)' does not exist in the current context/gu,
  );
}

function parseUndeclaredTypeNames(compilerOutput) {
  return parseNames(
    compilerOutput,
    /error CS0246: The type or namespace name '([^'<]+)(?:<[^']*)?' could not be found/gu,
  );
}

function translateDiagnostics(compilerOutput, sourcePath, lineMap, fileLabel) {
  const pattern = new RegExp(
    `${sourcePath.replace(/[.*+?^${}()|[\]\\]/gu, "\\$&")}\\((\\d+),\\d+\\): (error [^\\r\\n]+)`,
    "gu",
  );
  const translated = [];
  const seen = new Set();
  let match;

  while ((match = pattern.exec(compilerOutput)) !== null) {
    const generatedLine = Number(match[1]);
    const sourceLine = lineMap.get(generatedLine);
    const location = sourceLine === undefined ? `${fileLabel} (generated)` : `${fileLabel}:${sourceLine}`;
    const message = `${location} ${match[2].replace(/\s*\[[^\]]*\.csproj\]\s*$/u, "")}`;

    if (!seen.has(message)) {
      seen.add(message);
      translated.push(message);
    }
  }

  return translated.length > 0 ? translated : [`${fileLabel}: ${compilerOutput.trim()}`];
}

async function resolveDotnetInstallation() {
  const configuredRoot = process.env.DOTNET_ROOT;
  const configuredExecutable = configuredRoot === undefined ? undefined : join(configuredRoot, "dotnet");
  const dotnetPath =
    configuredExecutable !== undefined && existsSync(configuredExecutable) ? configuredExecutable : "dotnet";
  const { stdout: versionOutput } = await execFileAsync(dotnetPath, ["--version"], { cwd: process.cwd() });
  const version = versionOutput.trim();

  if (configuredRoot !== undefined) {
    return { dotnetPath, dotnetRoot: configuredRoot, version };
  }

  const { stdout: sdkOutput } = await execFileAsync(dotnetPath, ["--list-sdks"], { cwd: process.cwd() });
  const sdkEntry = sdkOutput
    .split(/\r?\n/gu)
    .map((line) => /^(\S+)\s+\[(.+)\]\s*$/u.exec(line))
    .find((match) => match?.[1] === version);

  if (sdkEntry === undefined) {
    throw new Error(`The active .NET SDK ${version} was not present in dotnet --list-sdks.`);
  }

  return { dotnetPath, dotnetRoot: dirname(sdkEntry[2]), version };
}

async function findReferenceAssemblies(dotnetRoot) {
  const files = [];

  // The ASP.NET Core pack is optional: samples that host the engine in a web app need it, and it
  // also carries the Microsoft.Extensions abstractions (hosting, logging, dependency injection).
  for (const packName of ["Microsoft.NETCore.App.Ref", "Microsoft.AspNetCore.App.Ref"]) {
    const refPacksRoot = join(dotnetRoot, "packs", packName);

    if (!existsSync(refPacksRoot)) {
      if (packName === "Microsoft.NETCore.App.Ref") {
        throw new Error(`Reference assembly pack not found under ${refPacksRoot}.`);
      }

      continue;
    }

    const versions = (await readdir(refPacksRoot)).filter((name) => name.startsWith("10."));

    if (versions.length === 0) {
      if (packName === "Microsoft.NETCore.App.Ref") {
        throw new Error(`No net10.0 reference pack found under ${refPacksRoot}.`);
      }

      continue;
    }

    const latest = versions.sort((left, right) => left.localeCompare(right, "en", { numeric: true })).at(-1);
    const refDirectory = join(refPacksRoot, latest, "ref", "net10.0");

    for (const name of await readdir(refDirectory)) {
      if (name.endsWith(".dll")) {
        files.push(join(refDirectory, name));
      }
    }
  }

  return files;
}

async function findCscPath(dotnetRoot, version) {
  const candidate = join(dotnetRoot, "sdk", version, "Roslyn", "bincore", "csc.dll");

  if (existsSync(candidate)) {
    return candidate;
  }

  throw new Error(`csc.dll not found for the active SDK version ${version} under ${join(dotnetRoot, "sdk")}.`);
}

/**
 * Collects every managed assembly produced by a Release build of each `src/*` project, then the
 * dependency assemblies that only executable hosts copy beside themselves (`examples/*`),
 * de-duplicated by file name with the first occurrence winning.
 * @param {string} repositoryRoot
 * @returns {Promise<string[]>}
 */
export async function findProjectAssemblies(repositoryRoot) {
  const byName = new Map();

  // Samples that show tests use the repository's test stack; only those assemblies are taken from
  // the test projects, never the code under test.
  const testStackAssemblies = new Set([
    "Shouldly.dll",
    "xunit.abstractions.dll",
    "xunit.v3.assert.dll",
    "xunit.v3.common.dll",
    "xunit.v3.core.dll",
  ]);

  for (const group of ["src", "examples", "tests"]) {
    const groupRoot = join(repositoryRoot, group);

    if (!existsSync(groupRoot)) {
      continue;
    }

    const projects = (await readdir(groupRoot, { withFileTypes: true })).filter((entry) => entry.isDirectory());

    for (const project of projects.sort((left, right) => left.name.localeCompare(right.name))) {
      const output = join(groupRoot, project.name, "bin", "Release", "net10.0");

      if (!existsSync(output)) {
        continue;
      }

      for (const file of await readdir(output)) {
        if (group === "tests" && !testStackAssemblies.has(file)) {
          continue;
        }

        if (file.endsWith(".dll") && !byName.has(file)) {
          byName.set(file, join(output, file));
        }
      }
    }
  }

  return [...byName.values()];
}

async function compileUnit(cscPath, dotnetPath, referenceAssemblies, projectAssemblies, sourcePath) {
  const responsePath = `${sourcePath}.rsp`;
  const referenceNames = new Set(referenceAssemblies.map((path) => basename(path)));
  const args = [
    "/nologo",
    "/target:library",
    "/langversion:14.0",
    "/nullable:enable",
    `/out:${sourcePath}.dll`,
    ...referenceAssemblies.map((path) => `/reference:${path}`),
    ...projectAssemblies
      .filter((path) => !referenceNames.has(basename(path)))
      .map((path) => `/reference:${path}`),
    sourcePath,
  ];

  await writeFile(responsePath, args.map((argument) => `"${argument}"`).join("\n"), "utf8");

  try {
    await execFileAsync(dotnetPath, [cscPath, `@${responsePath}`], { maxBuffer: 64 * 1024 * 1024 });
    return { success: true, output: "" };
  } catch (error) {
    return { success: false, output: `${error.stdout ?? ""}${error.stderr ?? ""}` };
  }
}

const declaredTypeName =
  /^\s*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|file|unsafe)\s+)*(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+(\w+)/mu;

/**
 * Collects the top-level type declarations of every document so a sample that mentions a type
 * another document declares (a shared policy, a helper) can borrow that declaration instead of
 * compiling against an empty stub.
 * @param {string[]} files
 * @returns {Promise<Map<string, string>>} Type name to declaration source; the first declaration in path order wins.
 */
async function collectDeclarationPool(files) {
  const pool = new Map();

  for (const file of files) {
    for (const block of extractCSharpBlocks(await readFile(file, "utf8"))) {
      if (block.skipped) {
        continue;
      }

      const stripped = stripUsingLines(block.code);

      if (isContractShapeBlock(stripped)) {
        continue;
      }

      for (const unit of splitBlock(stripped)) {
        const match = unit.kind === "declaration" ? declaredTypeName.exec(unit.code) : null;

        if (match === null) {
          continue;
        }

        if (!pool.has(match[1])) {
          pool.set(match[1], unit.code);
        }
      }
    }
  }

  return pool;
}

async function validateFile(context, file) {
  const { root, workingDirectory } = context;
  const content = await readFile(file, "utf8");
  const allBlocks = extractCSharpBlocks(content);
  const skippedCount = allBlocks.filter((block) => block.skipped).length;
  const blocks = allBlocks.filter((block) => !block.skipped);
  const usings = [
    ...context.usings,
    ...resolveAmbiguousTypeAliases(
      blocks
        .map((block) => block.code)
        .filter((code) => !isContractShapeBlock(code))
        .join("\n"),
      context.typeNamespaces,
    ),
  ];

  if (blocks.length === 0) {
    return { errors: [], blockCount: 0, skippedCount };
  }

  const fileLabel = relative(root, file);
  const stubNames = new Set();
  const stubTypeNames = new Set();
  const borrowedNames = new Set();
  const sourcePath = join(workingDirectory, `${sanitizeIdentifier(fileLabel)}.cs`);

  for (let attempt = 0; attempt <= maxStubIterations; attempt++) {
    const { source, lineMap } = renderCompilationUnit(
      fileLabel,
      blocks,
      stubNames,
      stubTypeNames,
      usings,
      [...borrowedNames].sort().map((name) => context.pool.get(name)),
    );
    await writeFile(sourcePath, source, "utf8");

    if (process.env.DOC_SAMPLES_DEBUG_DIR) {
      await writeFile(join(process.env.DOC_SAMPLES_DEBUG_DIR, `${sanitizeIdentifier(fileLabel)}.cs`), source, "utf8");
    }

    const result = await compileUnit(
      context.cscPath,
      context.dotnetPath,
      context.referenceAssemblies,
      context.projectAssemblies,
      sourcePath,
    );

    if (result.success) {
      if (process.env.DOC_SAMPLES_VERBOSE && (stubNames.size > 0 || stubTypeNames.size > 0)) {
        console.log(`${fileLabel}: names=[${[...stubNames]}] types=[${[...stubTypeNames]}]`);
      }

      return { errors: [], blockCount: blocks.length, skippedCount };
    }

    const newNames = [...parseUndeclaredNames(result.output)].filter((name) => !stubNames.has(name));
    const undeclaredTypes = [...parseUndeclaredTypeNames(result.output)].filter(
      (name) => !stubTypeNames.has(name) && !borrowedNames.has(name),
    );
    const newBorrowed = undeclaredTypes.filter((name) => context.pool.has(name));
    const newTypeNames = undeclaredTypes.filter((name) => !context.pool.has(name));

    if (newNames.length === 0 && newTypeNames.length === 0 && newBorrowed.length === 0) {
      if (process.env.DOC_SAMPLES_VERBOSE) {
        console.log(`${fileLabel}: FAILED names=[${[...stubNames]}] types=[${[...stubTypeNames]}]`);
      }

      return {
        errors: translateDiagnostics(result.output, sourcePath, lineMap, fileLabel),
        blockCount: blocks.length,
        skippedCount,
      };
    }

    for (const name of newNames) {
      stubNames.add(name);
    }

    for (const name of newTypeNames) {
      stubTypeNames.add(name);
    }

    for (const name of newBorrowed) {
      borrowedNames.add(name);
    }
  }

  return {
    errors: [`${fileLabel} failed to compile after ${maxStubIterations} stub iterations.`],
    blockCount: blocks.length,
    skippedCount,
  };
}

/**
 * Compiles every ```csharp block in the Markdown tree rooted at `root`, one shared compilation
 * per file so declaration blocks stay visible to later fragments in the same document. A sample
 * that references an undeclared simple name or type is retried with that name stubbed (see
 * `knownStubs`), up to `maxStubIterations` times, before being reported as a genuine failure.
 * @param {string} root Repository root holding `docs`, `src`, and the Markdown files.
 * @param {string[]} projectAssemblies Built AgentKit assemblies the samples reference.
 * @param {string[]} usings Namespaces imported by the harness.
 * @param {Map<string, Set<string>>} typeNamespaces Public type name to declaring namespaces, used to alias ambiguous names.
 * @returns {Promise<{errors: string[], totalBlocks: number, skippedBlocks: number}>}
 */
export async function validateDocSamples(root, projectAssemblies, usings, typeNamespaces = new Map()) {
  const files = await collectMarkdownFiles(root);
  const { dotnetPath, dotnetRoot, version } = await resolveDotnetInstallation();
  const referenceAssemblies = await findReferenceAssemblies(dotnetRoot);
  const cscPath = await findCscPath(dotnetRoot, version);
  const workingDirectory = await mkdtemp(join(tmpdir(), "agentkit-docs-samples-"));
  const pool = await collectDeclarationPool(files);
  const context = { root, workingDirectory, usings, typeNamespaces, pool, cscPath, dotnetPath, referenceAssemblies, projectAssemblies };
  const errors = [];
  let totalBlocks = 0;
  let skippedBlocks = 0;

  try {
    const queue = [...files];
    const workers = Array.from({ length: maxConcurrentCompilations }, async () => {
      for (let file = queue.shift(); file !== undefined; file = queue.shift()) {
        const result = await validateFile(context, file);
        totalBlocks += result.blockCount;
        skippedBlocks += result.skippedCount;
        errors.push(...result.errors);
      }
    });

    await Promise.all(workers);
  } finally {
    await rm(workingDirectory, { recursive: true, force: true });
  }

  return { errors: errors.sort(), totalBlocks, skippedBlocks };
}

async function main() {
  const root = process.cwd();
  const sourceRoot = resolve(root, "src");
  const projectAssemblies = await findProjectAssemblies(root);

  if (projectAssemblies.length === 0) {
    console.error("No Release build output found under src/*/bin/Release/net10.0. Build the solution in Release first.");
    process.exitCode = 1;
    return;
  }

  const { namespaces, typeNamespaces } = await scanPublicTypes(sourceRoot);
  const usings = [...systemUsings, ...namespaces];
  const { errors, totalBlocks, skippedBlocks } = await validateDocSamples(root, projectAssemblies, usings, typeNamespaces);

  if (errors.length === 0) {
    console.log(
      `All ${totalBlocks} documentation C# samples compile` +
        (skippedBlocks > 0 ? ` (${skippedBlocks} blocks skipped by an explicit doc-sample directive).` : "."),
    );
    return;
  }

  for (const error of errors) {
    console.error(error);
  }

  process.exitCode = 1;
}

const invokedPath = process.argv[1] === undefined ? undefined : pathToFileURL(resolve(process.argv[1])).href;

if (invokedPath === import.meta.url) {
  await main();
}
