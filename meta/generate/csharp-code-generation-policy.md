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
