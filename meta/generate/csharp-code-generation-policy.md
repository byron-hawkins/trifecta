*author: Copilot*

# C# code generation policy

Generated C# source MUST follow the repository's existing C# layout and
formatting conventions.

1. Generated files MUST be formatted with `clang-format` using the repository's
   applicable configuration.
2. In generated classes, constructors and public API members MUST appear before
   private implementation fields. Private fields MUST be grouped after the
   methods, following the layout used in `src/trifecta/io/ComponentLog.cs`.
3. Generated declarations MUST preserve the source model's names, types,
   modifiers, parameter defaults, and member behavior; formatting and member
   ordering MUST NOT change their semantics.
4. Shared test base classes MUST be placed in `test/<suite>/common` and use the
   `trifecta.test.common` namespace. Test cases MUST use namespaces that follow
   `trifecta.test.<area>`; for example, logging tests use
   `trifecta.test.io`.
5. Generated files MUST identify Copilot at the top, using the native comment
   or metadata syntax for their format. For C#:
   - A file containing only a generated stub MUST use `/* stub: Copilot */`.
   - A file authored or substantively changed by Copilot MUST use
     `/* author: Copilot */`, replacing a stub marker if work beyond stub
     generation is performed.
   - Markdown: visible italic attribution `*author: Copilot*`
   - BNF: `(* author: Copilot *)`
   - HTML and XML: `<!-- author: Copilot -->`
   - JSON Schema: a top-level `"$comment": "author: Copilot"`
   - SMD JSON: a top-level `"author": "Copilot"` property, permitted by its
     schema
6. Generated C# MUST use braced namespace declarations. Write one `namespace`
   keyword for each namespace segment, with each declaration having its own
   braces, following the style used in `src/`. For example:

   ```csharp
   namespace foo {
   namespace bar {

   // declarations

   }
   }
   ```
