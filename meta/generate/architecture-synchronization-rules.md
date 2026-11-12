*author: Copilot*

# Architecture synchronization rules

The C# source is the authority for the public members of each documented
type. When synchronizing architecture documents with `src/`, the corresponding
forward and reverse SMD types **must map their public members precisely
one-to-one by name**:

1. Every public member declared in the C# type must appear in its Markdown
   outline and forward SMD. If a member is missing, add it; leaving one out is
   a serious synchronization failure.
2. Every public member represented in the outline and forward SMD must
   correspond to a public member still declared in that C# type. Remove an
   obsolete public member from the outline when it has been removed from the
   source.
3. Regenerate the reverse SMD from the current source, so a member removed from
   C# is also removed from the reverse SMD. Do not retain stale reverse output.
4. Compare members within their corresponding types by kind and name,
   including constructors, fields, properties, methods, interface members and
   enum values. Account for overloads individually: the number of entries
   for each overloaded name must match, with no duplicates or omissions.
   Compare declared members, not members merely inherited from another type.
5. Exact signatures are **not** required: parameter names, parameter lists
   and other signature details may be simplified. Distinguish overloads enough
   to establish their one-to-one correspondence; do not treat a signature-only
   difference as a missing or extra member.

Private and other non-public members may be included in an outline for
architecture purposes; the one-to-one rule above applies to public members.
Interface members are public unless explicitly declared otherwise.

## Procedure

1. Forward-compile the current annotated Markdown to
   `doc/architecture/bin/*.smd`, then reverse-compile the current source to
   `doc/architecture/reverse/*.smd`.
2. Compare the public members of **every** documented type, including member
   kinds and overload counts. Add missing members and remove obsolete public
   members in the Markdown outlines; do not edit generated SMD as the sole
   fix.
3. Regenerate the forward SMD, affected diagrams and rendered pages, and
   regenerate the reverse SMD after source changes. Repeat the comparison
   until no public members are missing or extra, and validate the generated
   SMD against the schema.
4. Treat missing types, ambiguous overload correspondence, unsupported public
   declarations or other blockers as unresolved: report them and ask for
   guidance rather than claiming synchronization is complete.

The reverse compiler's count of types found and its diagnostics do **not** by
themselves compare the two public-member lists. A successful reverse compile
alone does not establish one-to-one correspondence.
