# Collaboration transcript — 2026-11-08

## Summary

This session established a workflow for turning annotated architecture Markdown
into a structured intermediate representation (SMD), generating Mermaid
diagrams, and producing browser-viewable rendered documentation. The work
focused on making the annotation rules explicit, keeping generated output
deterministic, and testing the diagram syntax against actual renderers.

## Decisions and design

- The architecture annotation format consists of a BNF grammar plus a separate
  normative semantics document. The grammar defines syntax; the semantics
  document defines context scope, interpretation, generation rules, and stable
  serialization requirements.
- A namespace context remains active until the next namespace context tag. A
  class context ends at the earlier of the next class context tag or an
  unannotated level-two heading. Type annotations associate with all active
  contexts.
- Type-outline categories include `types`, `composition`, `operations`,
  `inherits`, and `capabilities`. Operations are represented as actual method
  or constructor members in generated diagrams—not as a category label.
- `[singleton]` is type-level intent represented in composition. It denotes a
  public static singleton instance defined within the class, and in a diagram
  it is displayed simply as `singleton`, without a field descriptor.
- A `System.Threading.Lock _lock` is now an ordinary explicitly named field;
  `[lock]` is not a special annotation.
- Interface relationships are represented in IR as `base_types` with
  `relation: implements`; base classes and interface extension use
  `relation: extends`.
- The SMD format is JSON validated against a Draft 2020-12 JSON Schema.
  Deterministic output rules cover property ordering, source-order arrays,
  sorted set-like arrays, deterministic IDs, relative paths, UTF-8, two-space
  indentation, LF line endings, and a final newline.
- The named workflow is `diagram-and-render <source.md>`. It generates
  `doc/architecture/bin/<basename>.smd`, diagram files at paths declared by
  directives, rendered Markdown under the sibling `render/` directory, and
  sibling HTML suitable for direct browser viewing.

## Issues encountered and resolutions

- Initial grammar reviews identified missing document-level parsing rules,
  unclear annotation boundaries, and a missing blank-line terminator for type
  annotations. These were clarified in the evolving BNF.
- Context scope initially risked associating later interface annotations with
  an earlier class context. The class context boundary was clarified to stop at
  the next unannotated level-two heading.
- The proposed schema initially allowed unnamed parameters, but the Markdown
  signatures were updated to include names. The schema now requires parameter
  names, and the generated SMD stores them.
- Mermaid rendering failed with an `interface` keyword declaration. Mermaid
  class diagrams require an interface represented as a `class` block with an
  `<<interface>>` stereotype. Adding semicolon terminators was tried and did
  not resolve the issue; the semicolons were removed because Mermaid uses
  closing braces and line breaks.
- The generated HTML initially pinned Mermaid 11.16.0. At the user's request,
  the pin was removed; the HTML now loads the current Mermaid build from
  jsDelivr. The page therefore requires an internet connection.

## Outputs and validation

- `meta/grammar/markdown-architecture.bnf` — annotation grammar, including
  separate productions for field, method, constructor, parameter, and
  relationship forms.
- `meta/grammar/markdown-architecture-semantics.md` — normative scope,
  annotation, diagram, workflow, and stable serialization rules.
- `meta/grammar/architecture-schema.json` — JSON Schema for SMD and documented
  Markdown/C# mapping guidance.
- `doc/architecture/bin/io.smd` — structured representation generated from
  `doc/architecture/io.md`.
- `doc/architecture/figures/io.logging.md` — Mermaid class diagram for
  `ComponentLog` and `ITextLogSink`.
- `doc/architecture/render/io.logging.md` — source Markdown with the diagram
  directive replaced by Mermaid.
- `doc/architecture/render/io.logging.html` — browser-viewable HTML with the
  Mermaid diagram embedded.

The SMD was validated with Python `jsonschema` against the schema, including
schema self-check and reference checks. The generated HTML was opened in the
integrated browser, where the Mermaid diagram rendered successfully. Mermaid
CLI is not installed; no local offline rendering test was performed.

## Advantages achieved

- Architecture annotations now have explicit syntax and normative semantics,
  reducing ambiguity for future agents and tools.
- The SMD separates parsed structure from Markdown presentation, providing a
  common input for diagram generation and future consumers such as test-mock
  or stub generation.
- Deterministic serialization rules make generated files more reviewable and
  reduce noisy diffs.
- Diagram generation is tied to explicit type selection and namespace scope,
  avoiding unrelated types appearing in a diagram.
- The HTML output enables direct browser viewing without depending on a
  Markdown preview plugin.
- The Mermaid syntax issue was investigated and corrected based on the
  renderer's parser feedback.

## Follow-up notes

- The current workflow semantics say the rendered Markdown filename is based
  on the diagram output filename. For `figures/io.logging.md`, this is
  `render/io.logging.md`; the HTML sibling is `render/io.logging.html`.
- The browser HTML uses the unpinned CDN URL
  `https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js`, so it needs
  network access. A future offline setup can bundle Mermaid locally or use
  Mermaid CLI (`mmdc`) for static SVG/PNG output.
- Some diagram details remain intentionally open, including the exact edge
  mapping for `composition` and `inherits`, treatment of unmatched requested
  types, and behavior for annotation-like text inside code examples.
- When Mermaid CLI is installed, validate generated diagrams locally and
  consider updating the workflow to support static rendering or fully offline
  HTML.

---

# Collaboration transcript — 2026-11-08 (continued)

## Summary

The architecture outline for `environment.md` was converted into SMD, Mermaid
diagrams, and rendered Markdown/HTML. C# stubs and tests were added for
`SensorInterpreter`. The test workflow was clarified: tests with matching
toddler and macarena copies refer to the macarena copy, toddler tests are
preserved unless specifically removed, and generated tests use the shared
test framework. A bootstrap guide was added to identify session-essential
workspace files.

## Decisions and design

- `SensorInterpreter` test coverage was split into two phases: toddler tests
  validate the API and demonstrate expected behavior against stubs; macarena
  tests compile against source implementations and exercise their behavior.
- Once a test has a macarena copy, future requests for that test refer to the
  macarena copy. Toddler tests must not be deleted without explicit instruction,
  even when they become obsolete.
- Tests use the shared test framework in the applicable suite's `common/`
  directory.
- C# generation attribution distinguishes stub-only files
  (`/* stub: Copilot */`) from files authored or substantively changed by
  Copilot (`/* author: Copilot */`).
- Generated C# follows the braced namespace style used in `src/`, with one
  `namespace` keyword and its own braces for each namespace segment.
- C# `using` aliases are lexical and are not inherited from interfaces.
  Interfaces may declare nested record structs, but those types are public
  members qualified through the interface name, not aliases inherited by
  implementers.

## Issues encountered and resolutions

- The first macarena `ComponentLog` test build exposed a missing separator in
  the logger's release path. The source is in a read-only mount from this
  workspace, so that edit could not be made here. After the source was fixed
  externally, the macarena `ComponentLog` test passed.
- Macarena SensorInterpreter tests initially failed to compile because the
  real source had incorrect `Stable` member references, inaccessible fields,
  and unresolved `Array` references. After the source was updated, the tests
  compiled and ran.
- The macarena threshold-crossing test failed because the recorded events
  differed from the expected crossings. The update/readback/logging test also
  failed: no matching update log entries were found for either sensor type.
- The first macarena test invocation with `--no-restore` lacked the xUnit
  execution runtime in the output directory. Running without `--no-restore`
  restored the test dependencies and allowed test discovery and execution.
- The toddler SensorInterpreter tests compile and fail as expected against the
  stub: the threshold test records no events, while the readback/log test sees
  default getter values and no log file.

## Outputs and validation

- `doc/architecture/bin/environment.smd` — SMD for the sensor architecture;
  schema constraints and cross-references were checked.
- `doc/architecture/figures/sensor-awareness.md` and
  `doc/architecture/figures/sensor-control.md` — generated Mermaid diagrams.
- `doc/architecture/render/sensor-awareness.md`,
  `doc/architecture/render/sensor-control.md`, and their HTML siblings —
  rendered documents preserving source annotations. The browser displayed both
  Mermaid diagrams as SVGs without errors.
- `test/toddler/trifecta/environment/Environment.cs` and
  `test/toddler/trifecta/environment/SensorInterpreter.cs` — environment
  interface and interpreter stubs.
- `test/toddler/environment/SensorInterpreterTests.cs` — API/readback/logging
  and threshold-crossing tests against the toddler stubs.
- `test/macarena/trifecta/environment/SensorInterpreterTests.cs` — corresponding
  macarena tests against the real environment sources.
- `test/macarena/Trifecta.Macarena.Tests.csproj` — links
  `src/trifecta/environment/*.cs` for macarena test builds.
- `test/macarena/trifecta/io/ComponentLogTests.cs` — checks the component
  release log message; the focused test passed after the logger source fix.
- `meta/test/testing-rules.md` — test-copy precedence, test-preservation, and
  shared-framework rules.
- `meta/generate/csharp-code-generation-policy.md` — updated C# author/stub
  attribution and namespace-style rules.
- `agent/agent-bootstrap.md` — session startup reading list and maintenance
  rule for newly created session-essential files.

## Advantages achieved

- The toddler/macarena distinction is explicit: bootstrap tests validate
  signatures and expected cases, while macarena tests exercise actual source
  behavior.
- The `ComponentLog` release message now has a passing macarena regression
  test.
- SensorInterpreter's threshold and logging behavior has test coverage that
  exposes differences between expected and actual implementation behavior.
- Session startup expectations and the rule for maintaining the bootstrap list
  are documented.

## Follow-up notes

- Resolve the macarena SensorInterpreter event mismatch and missing update log
  entries, then rerun the focused macarena tests.
- Preserve toddler tests unless explicitly instructed otherwise.
- Keep `agent/agent-bootstrap.md` updated whenever a session-essential file is
  created.
