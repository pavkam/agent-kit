import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  classifyBlock,
  extractCSharpBlocks,
  isContractShapeBlock,
  renderCompilationUnit,
  resolveAmbiguousTypeAliases,
  splitBlock,
  stripUsingLines,
  validateDocSamples,
} from "./validate-doc-samples.mjs";

async function withDotnetRoot(installation, action) {
  const previousDotnetRoot = process.env.DOTNET_ROOT;

  try {
    process.env.DOTNET_ROOT = installation;
    await action();
  } finally {
    if (previousDotnetRoot === undefined) {
      delete process.env.DOTNET_ROOT;
    } else {
      process.env.DOTNET_ROOT = previousDotnetRoot;
    }
  }
}

test("validateDocSamples_WhenReferencePacksHaveMultiDigitPatch_SelectsLatestVersion", async () => {
  const root = await mkdtemp(join(tmpdir(), "agentkit-doc-samples-test-"));
  const installation = join(root, "dotnet");
  const activeSdk = execFileSync("dotnet", ["--version"], { encoding: "utf8" }).trim();

  try {
    await mkdir(join(installation, "packs", "Microsoft.NETCore.App.Ref", "10.0.9"), { recursive: true });
    await mkdir(join(installation, "packs", "Microsoft.NETCore.App.Ref", "10.0.10", "ref", "net10.0"), {
      recursive: true,
    });
    const cscDirectory = join(installation, "sdk", activeSdk, "Roslyn", "bincore");
    await mkdir(cscDirectory, { recursive: true });
    await writeFile(join(cscDirectory, "csc.dll"), "", "utf8");

    await withDotnetRoot(installation, async () => {
      const result = await validateDocSamples(root, []);

      assert.deepEqual(result, { errors: [], totalBlocks: 0, skippedBlocks: 0 });
    });
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("extractCSharpBlocks_WhenFenceIsCSharp_ReturnsBodyAndStartLine", () => {
  const content = ["# Title", "", "```csharp", "var x = 1;", "```", "", "```bash", "dotnet build", "```"].join("\n");

  const blocks = extractCSharpBlocks(content);

  assert.equal(blocks.length, 1);
  assert.equal(blocks[0].code, "var x = 1;");
  assert.equal(blocks[0].startLine, 4);
  assert.equal(blocks[0].skipped, false);
});

test("extractCSharpBlocks_WhenNoFencesPresent_ReturnsEmpty", () => {
  assert.deepEqual(extractCSharpBlocks("# Title\n\nSome prose.\n"), []);
});

test("extractCSharpBlocks_WhenSkipDirectivePrecedesFence_MarksOnlyThatBlockSkipped", () => {
  const content = [
    "<!-- doc-sample: skip - excerpt of the example application -->",
    "",
    "```csharp",
    "var a = 1;",
    "```",
    "",
    "```csharp",
    "var b = 2;",
    "```",
  ].join("\n");

  const blocks = extractCSharpBlocks(content);

  assert.deepEqual(
    blocks.map((block) => block.skipped),
    [true, false],
  );
});

test("extractCSharpBlocks_WhenSkipDirectiveHasNoReason_DoesNotSkip", () => {
  const content = ["<!-- doc-sample: skip - -->", "```csharp", "var a = 1;", "```"].join("\n");

  assert.equal(extractCSharpBlocks(content)[0].skipped, false);
});

test("stripUsingLines_WhenUsingLinesPresent_RemovesOnlyThose", () => {
  const code = ["using System;", "using AgentKit.Tools;", "var x = 1;"].join("\n");

  assert.equal(stripUsingLines(code), "var x = 1;");
});

test("isContractShapeBlock_WhenFileScopedNamespaceDeclared_ReturnsTrue", () => {
  assert.equal(isContractShapeBlock("namespace AgentKit.Tools;\n\npublic interface IFoo { }"), true);
  assert.equal(isContractShapeBlock("var x = 1;"), false);
});

test("classifyBlock_WhenBodyStartsWithTypeKeyword_ReturnsDeclaration", () => {
  assert.equal(classifyBlock("public sealed class Tile\n{\n}\n"), "declaration");
});

test("classifyBlock_WhenBodyStartsWithMemberModifier_ReturnsMember", () => {
  assert.equal(classifyBlock("public int Fill { get; set; } = 3;"), "member");
});

test("classifyBlock_WhenBodyIsPlainStatements_ReturnsFragment", () => {
  assert.equal(classifyBlock('var result = await engine.AskAsync("hi");'), "fragment");
});

test("classifyBlock_WhenOnlyBlankOrComments_ReturnsFragment", () => {
  assert.equal(classifyBlock("\n// nothing here\n"), "fragment");
});

test("splitBlock_WhenStatementsPrecedeTrailingTypeDeclaration_SeparatesUnitsByKind", () => {
  const body = [
    "var status = Run(new Hello());",
    "return status;",
    "",
    "sealed class Hello",
    "{",
    "}",
  ].join("\n");

  const units = splitBlock(body);

  assert.deepEqual(
    units.map((unit) => unit.kind),
    ["statement", "statement", "declaration"],
  );
  assert.match(units[2].code, /sealed class Hello/u);
});

test("splitBlock_WhenMemberHasNoBody_ReturnsSignature", () => {
  const units = splitBlock("public Task RunAsync(\n    string name,\n    int count = 3);");

  assert.deepEqual(
    units.map((unit) => unit.kind),
    ["signature"],
  );
});

test("splitBlock_WhenMemberHasExpressionBody_ReturnsMember", () => {
  const units = splitBlock("public int Twice(int value) => value * 2;");

  assert.deepEqual(
    units.map((unit) => unit.kind),
    ["member"],
  );
});

test("renderCompilationUnit_WhenBlockHasStatements_WrapsThemInAHarnessMethod", () => {
  const blocks = [{ code: "var x = 1;\nConsole.WriteLine(x);", startLine: 10 }];

  const { source, lineMap } = renderCompilationUnit("docs/guide.md", blocks, new Set(), new Set());

  assert.match(source, /namespace Docs\./u);
  assert.match(source, /internal async global::System\.Threading\.Tasks\.Task Fragment_0\(\)/u);
  assert.ok([...lineMap.values()].includes(10));
});

test("renderCompilationUnit_WhenSnippetReturnsAValue_UsesAnIntegerTask", () => {
  const blocks = [{ code: "if (failed)\n{\n    return 2;\n}\n\nreturn 0;", startLine: 1 }];

  const { source } = renderCompilationUnit("docs/guide.md", blocks, new Set(), new Set());

  assert.match(source, /Task<int> Fragment_0\(\)/u);
});

test("renderCompilationUnit_WhenReturnIsInsideALambda_KeepsAPlainTask", () => {
  const blocks = [{ code: "app.Map(async () =>\n{\n    return 1;\n});", startLine: 1 }];

  const { source } = renderCompilationUnit("docs/guide.md", blocks, new Set(), new Set());

  assert.doesNotMatch(source, /Task<int>/u);
});

test("renderCompilationUnit_WhenDeclarationBlockPresent_EmitsItAtNamespaceScope", () => {
  const blocks = [{ code: "public sealed class Tile\n{\n}\n", startLine: 3 }];

  const { source, lineMap } = renderCompilationUnit("docs/guide.md", blocks, new Set(), new Set());

  assert.match(source, /public sealed class Tile/u);
  assert.ok(lineMap.size > 0);
});

test("renderCompilationUnit_WhenBlockIsContractShape_DoesNotCompileIt", () => {
  const blocks = [{ code: "namespace AgentKit.Tools;\n\npublic interface IFoo\n{\n    void Bar();\n}", startLine: 1 }];

  const { source } = renderCompilationUnit("docs/guide.md", blocks, new Set(), new Set());

  assert.doesNotMatch(source, /IFoo/u);
});

test("renderCompilationUnit_WhenKnownStubNameGiven_UsesTheTypedStubOverDynamic", () => {
  const blocks = [{ code: "Use(apiKey);", startLine: 5 }];

  const { source } = renderCompilationUnit("docs/guide.md", blocks, new Set(["apiKey", "other"]), new Set());

  assert.match(source, /internal static string apiKey = "";/u);
  assert.match(source, /internal static dynamic other = default!;/u);
});

test("renderCompilationUnit_WhenStubTypeAndBorrowedDeclarationGiven_EmitsBothInTheGlobalNamespace", () => {
  const blocks = [{ code: "var x = new Missing();", startLine: 1 }];

  const { source } = renderCompilationUnit(
    "docs/guide.md",
    blocks,
    new Set(),
    new Set(["Missing"]),
    ["System"],
    ["sealed class Borrowed { }"],
  );

  assert.match(source, /public sealed class Missing \{ \}/u);
  assert.match(source, /sealed class Borrowed \{ \}/u);
});

test("renderCompilationUnit_WhenBlockContinuesABuilderChain_StartsFromAnEngineBuilder", () => {
  const blocks = [{ code: '.WithInstructions("hi")\n.WithMaxTurns(3)', startLine: 1 }];

  const { source } = renderCompilationUnit("docs/guide.md", blocks, new Set(), new Set());

  assert.match(source, /_ = global::AgentKit\.AgentEngine\.CreateBuilder\(\)/u);
  assert.match(source, /\.WithMaxTurns\(3\);/u);
});

test("resolveAmbiguousTypeAliases_WhenNameIsDeclaredInTwoNamespaces_PicksTheNamespaceOfTheOtherTypes", () => {
  const typeNamespaces = new Map([
    ["OpenMode", new Set(["AgentKit.Alpha.Sqlite", "AgentKit.Beta.Sqlite"])],
    ["BetaTarget", new Set(["AgentKit.Beta.Sqlite"])],
    ["AlphaTarget", new Set(["AgentKit.Alpha.Sqlite"])],
  ]);

  const aliases = resolveAmbiguousTypeAliases("new BetaTarget(OpenMode.Create);", typeNamespaces);

  assert.deepEqual(aliases, ["OpenMode = AgentKit.Beta.Sqlite.OpenMode"]);
});

test("resolveAmbiguousTypeAliases_WhenNameIsUnambiguous_ReturnsNoAlias", () => {
  const typeNamespaces = new Map([["Tile", new Set(["AgentKit.Alpha"])]]);

  assert.deepEqual(resolveAmbiguousTypeAliases("new Tile();", typeNamespaces), []);
});

async function validateMarkdown(markdown) {
  const root = await mkdtemp(join(tmpdir(), "agentkit-doc-samples-test-"));

  try {
    await mkdir(join(root, "docs"), { recursive: true });
    await writeFile(join(root, "docs", "sample.md"), markdown, "utf8");

    return await validateDocSamples(root, [], ["System"]);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
}

test("validateDocSamples_WhenSampleCompiles_ReportsNoErrors", async () => {
  const result = await validateMarkdown("# Sample\n\n```csharp\nvar total = 1 + 2;\nConsole.WriteLine(total);\n```\n");

  assert.deepEqual(result, { errors: [], totalBlocks: 1, skippedBlocks: 0 });
});

test("validateDocSamples_WhenSampleDoesNotCompile_ReportsTheMarkdownLine", async () => {
  const result = await validateMarkdown("# Sample\n\n```csharp\nint total = \"three\";\n```\n");

  assert.equal(result.errors.length, 1);
  assert.match(result.errors[0], /^docs[\\/]sample\.md:4 error CS0029/u);
});

test("validateDocSamples_WhenBlockIsSkippedByDirective_CountsItAndDoesNotCompileIt", async () => {
  const result = await validateMarkdown(
    "<!-- doc-sample: skip - pseudocode -->\n\n```csharp\nint total = \"three\";\n```\n",
  );

  assert.deepEqual(result, { errors: [], totalBlocks: 0, skippedBlocks: 1 });
});
