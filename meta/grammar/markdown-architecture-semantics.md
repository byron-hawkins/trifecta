*author: Copilot*

# Markdown architecture annotation semantics

This document defines the meaning of annotations parsed using
[`markdown-architecture.bnf`](./markdown-architecture.bnf). The grammar defines
their syntax; these rules define how a parser associates and uses them.
The serialized IR MUST conform to [`architecture-schema.json`](./architecture-schema.json).

The terms **MUST**, **MUST NOT**, and **MAY** are normative.

## 1. Document parsing

1. A parser MUST parse the document as a whole according to `<document>`. It
   MUST NOT require a separate scan-and-parse pass to locate annotations.
2. Markdown outside annotations is preserved as document content. An annotation
   is recognized only where the grammar permits it, including its required
   preceding newline.
3. This specification does not define special treatment for annotation-looking
   text in fenced code blocks, inline code, or examples. Such text is subject to
   the grammar like other document text.

## 2. Context association

1. A context tag takes effect at its position in the document. Its scope includes
   subsequent Markdown and annotations until its scope ends.
2. A namespace context remains active until the next namespace context tag. A
   class context tag MUST NOT end the active namespace context.
3. A class context remains active until the earlier of:
   - the next class context tag; or
   - the next unannotated level-two Markdown heading.
4. A level-two heading associated with a class context tag is not an
   unannotated heading for the purpose of ending that context. A heading is
   associated with a class context tag when that tag precedes the heading with
   only whitespace between them.
5. A type annotation is associated with every namespace and class context active
   at the position of its `@type` tag. A type annotation outside any active
   context has no context association.
6. A new class context replaces the previous active class context; it does not
   end the active namespace context.
7. When a type annotation occurs under an active class context and its declared
   type name differs from that context's class name, the declared type is nested
   directly within the type named by that class context. The SMD MUST set
   `containing_type_id` to the containing type's ID. A class-context type
   annotation whose declared name matches the context name describes the
   containing type itself and MUST NOT have `containing_type_id`.

## 3. Type outline meaning

1. The declaration in a type outline identifies the type described by that
   annotation. Its categories and entries are metadata belonging to that type.
   Supported declaration kinds are class, interface, struct, and enum.
2. Generic declaration parameters MUST be recorded in `type_parameters` in
   source order. Generic base-type arguments MUST be recorded in
   `base_types[].type_arguments` as structured type references.
3. `types` records type-related declarations, such as aliases or nested types.
4. `composition` records members or parts of the annotated class or struct.
   - `[singleton]` denotes a public static singleton instance of the
     annotated class, defined within that class. A representative declaration
     is `public static Type singleton = new()`, where `Type` is the annotated
     class. When rendered in a class diagram, it MUST be shown as the word
     `singleton`, without a field descriptor.
   - A type name in a composition entry that does not resolve to a type
     annotation in the document MUST be assumed to name an existing type. The
     entry denotes a private instance field of that type, unless modifiers in
     the entry indicate otherwise. Consumers MUST NOT generate a separate
     diagram type element for that unresolved type; the entry is a named field
     type that readers are expected to recognize.
   - When the annotated declaration is an enum, each composition entry is an
     enum constant. It MUST be represented as an enum member, not as a field.
5. An enum declaration has no class/struct composition fields or operations; its
   composition entries are the enum constants.
6. `operations` records callable operations of the annotated type.
   Generic method parameters MUST be recorded on that method's
   `type_parameters`, independently of any type parameters declared by the
   containing type. Type references to a method parameter resolve in the
   method's generic scope; type references to a containing type parameter
   resolve in the containing type's scope.
7. `inherits` records base types or interfaces of the annotated type.
8. `capabilities` records interfaces implemented by the annotated type. In the
   IR, each such entry MUST be represented in `base_types` with
   `relation: implements`.
9. `[singleton]` has the meaning defined for composition entries above. It is
   not a standalone type declaration.

## 4. Diagram directives

1. A diagram directive selects the listed types for diagram generation. Type
   names in the directive are resolved against type annotations in the parsed
   document. A generic type selection MUST use its definition's type parameter
   names in declaration order (for example, `interface ISensorDriverActive<T>`),
   not concrete type arguments.
2. The directive's `<file-path>` is relative to the containing document's path.
   The generated Mermaid diagram MUST be written at that path.
3. A rendered copy of the source document MUST be written to
   `doc/architecture/render/io.md`, with each diagram directive replaced by the
   Mermaid diagram generated for that directive.
4. A generated class diagram MUST group selected types by their associated
   namespace using Mermaid namespace blocks. A namespace block MUST be emitted
   only when at least one type from that namespace appears in the diagram; types
   not selected by the directive MUST NOT cause a namespace or diagram element
   to be emitted.
5. A composition entry that names a type without a matching type annotation
   MUST NOT cause a separate diagram type element to be generated. The entry
   MAY be shown as a member of its annotated class when the Mermaid mapping
   supports it.
6. `operations` entries MUST be rendered as method or constructor members of
   the owning type. The category name `operations` MUST NOT be rendered as a
   label in the diagram.
7. An operation parsed as a method MUST be rendered using its method name,
   parameter names and types, and return type. It MUST appear in the type's
   methods compartment when the diagram format provides one.
8. A Mermaid class diagram MUST represent an interface using a `class` block
   with the `<<interface>>` stereotype; it MUST NOT use an `interface` keyword
   declaration.
9. Mermaid class bodies MUST be terminated by `}`. A closing brace MUST be
   followed by the next declaration on a new line; semicolon terminators MUST
   NOT be added.
10. A Mermaid class diagram MUST represent an enum using a `class` block with
    the `<<enumeration>>` stereotype, with its enum constants listed as members.
11. A Mermaid class diagram MUST represent a struct using a `class` block with
    the `<<struct>>` stereotype.
12. A selected generic type MUST be rendered as its generic definition, using
   Mermaid's `~` delimiters for its type parameters (for example,
   `class ISensorDriverActive~T~`). Member signatures MUST retain those
   parameter names. A generic type MUST NOT be expanded into separate concrete
   instantiations unless concrete type arguments are explicitly requested by a
   future rule.
13. The representation of the generated diagram file (raw Mermaid source or a
   fenced block), and the behavior for a requested type that has no matching
   annotation remain unspecified.

## 5. `diagram-and-render` workflow

1. The workflow is named `diagram-and-render` and takes one annotated Markdown
   source document as input.
2. The workflow MUST generate that document's SMD as
   `doc/architecture/bin/<source-basename>.smd`, where `<source-basename>` is
   the source filename without its `.md` extension.
3. For each diagram directive, the workflow MUST generate its Mermaid diagram
   at the directive's `<file-path>`, resolved relative to the source document's
   directory.
4. For each diagram directive, the workflow MUST generate a rendered copy of
   the source document under the source document's sibling `render/` directory.
   The rendered filename MUST be the basename of the diagram directive's
   `<file-path>`. For example, `figures/io.logging.md` produces
   `render/io.logging.md`.
5. In each rendered copy, every diagram annotation in the source MUST be
   replaced by its generated Mermaid diagram. All other Markdown content MUST
   be preserved.
6. If two directives would produce the same rendered filename, the workflow
   MUST report a collision rather than overwrite one output with another.
7. For each rendered Markdown file, the workflow MUST also emit a sibling HTML
   file with the same basename and `.html` extension.
8. The HTML file MUST contain the rendered document content, including each
   generated diagram as a Mermaid diagram element, and MUST be directly
   viewable in a browser without a Markdown-rendering extension.
9. The HTML output MUST load the current Mermaid release from a CDN, without
   pinning a specific version.
10. The HTML output MAY load Mermaid from a public CDN. If it does, the page
    requires network access to render diagrams; this dependency MUST be stated
    in the generated page.

## 6. Open semantics

The following details require additional rules before consumers can behave
identically:

- Whether annotation-looking text inside Markdown code or examples is active.
- The diagram edge and label mapping for `composition` and `inherits`.
- Whether unresolved or duplicate type names are errors, warnings, or ignored.
- The exact contents and formatting of generated Mermaid files and rendered
  documents.

## 7. IR serialization and stable diffs

1. An `.smd` file MUST be UTF-8 JSON. Its top-level `author`, `format`, and
   `version` fields MUST identify the generator as `Copilot` and the format as
   `markdown-architecture-smd`, version `1`.
   `source.path` MUST be relative to the workspace root and use `/` separators.
2. JSON objects MUST be serialized with properties in the order declared for
   that object in `architecture-schema.json`. Extension properties, if allowed
   by a later schema version, MUST follow defined properties in Unicode
   code-point order.
3. JSON arrays MUST use these orders:
   - `blocks`, top-level `contexts`, `types`, `diagrams`, each type's
     `contexts`, `base_types`, `aliases`, and `members`, diagram `types`,
     method and constructor `parameters`, and type arguments preserve source
     order.
   - Modifier arrays, member `annotations`, property `accessors`, and parameter
     modifiers are sets and MUST be sorted in ascending Unicode code-point
     order.
   - `diagnostics` are ordered by source position (line, then column); entries
     without a source position follow positioned entries and are ordered by
     `code`, then `message`.
4. The serializer MUST use two spaces per indentation level, LF line endings,
   and exactly one final LF.
5. An SMD generated by `diagram-and-render` MUST be written to
   `doc/architecture/bin/<source-basename>.smd`, using the source basename rule
   defined in Section 5.
6. IDs MUST be deterministic for the same source and mapping rules:
   - A type ID is `type:` followed by its fully qualified name. Nested type
     names use `+` between containing and nested type names. A generic type
     appends a backtick and its parameter count to its name in the ID (for
     example, `ISensorDriverPassive\`1`).
   - A context ID is `context:` followed by its kind, name, and one-based
     occurrence number among context tags with the same kind and name in the
     source, separated by colons.
   - A diagram block's `diagram_index` is its zero-based position in the
     `diagrams` array.
7. Serializers MUST NOT emit timestamps, random identifiers, machine-specific
   absolute paths, or other environment-dependent values.
