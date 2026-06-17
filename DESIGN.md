# Language Design Document

## Overview

A compiler for an unnamed programming language built around three file types: grammar files for extensible syntax, build files for project configuration, and script files for program logic. The compiler produces bytecode for a VM or native assembly.

## File Types

Grammar files define parsing rules and syntax extensions. They use the suffix `.grammar` for rules that apply to all file types, `.build.grammar` for build-file-only rules, and `.script.grammar` for script-file-only rules.

Build files declare package information, dependencies, and build steps. They use the full language syntax but are self-contained with no external imports. The `using` declaration is only valid in build files. A `Main` function serves as the entry point; exit code 0 continues compilation, non-zero aborts. Libraries omit `Main` but still declare dependencies.

Script files contain program logic. They are parsed using the combined grammar from core and all project/dependency grammar files.

## Grammar Syntax

Grammar atoms use BNF-inspired syntax with extensions for capture semantics.

`$NAME` references a terminal or atom but discards it from the AST. Use this for structural tokens like parentheses and keywords.

`@NAME` references and captures into the AST with auto-naming.

`@NAME:alias` references and captures with a custom field name.

Bare text represents literals, and must be identifier-like: `using`, `print`, `package`. Literals containing meta-characters must be single-quoted: `'('`, `':='`, `'+'`. (Unquoted `(` is the grouping operator.) An identifier-like literal becomes a reserved word: `print` cannot be used as a variable name.

Grouping uses parentheses. `?` marks optional, `*` marks zero or more, `+` marks one or more. Alternation uses `|` inline or as separate rules for the same atom. Captures inside groups are not yet supported; extract the group into its own atom instead.

Captured values become fields on the produced node. Unaliased captures auto-name from the referenced atom (`@STATEMENT*` → `statement` list field); when auto-names collide in one production they get positional suffixes (`@EXPR '+' @EXPR` → `expr0`, `expr1`). A production whose only capture is unaliased, unquantified, and alone (everything else discarded) passes the child node through instead of wrapping it — this is how `EXPR` wrapper rules and parens vanish from the tree.

Terminal classes are built into the lexer and referenced by name: `NUMBER`, `FLOAT`, `IDENTIFIER`, `VERSION` (`v1.0`), `NEWLINE`. A class is only lexed when some active grammar rule references it — `VERSION` exists for build files without polluting script lexing. The lexer is otherwise grammar-driven: literal terminals are collected from the active grammar, symbol literals match longest-first (`:=` before `:`), and identifier-like literals are checked after a full identifier scan (`print` is a keyword, `printx` is an IDENTIFIER). Statements are newline-terminated via `$NEWLINE`; the lexer collapses blank lines and injects a final NEWLINE so trailing newlines are optional.

Precedence uses annotations rather than stratified grammar. The `%N` suffix declares precedence level where higher numbers bind tighter. Associativity is declared with `%left`, `%right`, or `%none`.

Example (from the implemented core script grammar — each operator is its own atom so later
stages can switch on the node's atom name rather than a production index):
```
EXPR := @ADD | @SUB | @MUL | @DIV | @PAREN | @NUMBER | @FLOAT | @IDENTIFIER

ADD := @EXPR:lhs '+' @EXPR:rhs   %left %1
SUB := @EXPR:lhs '-' @EXPR:rhs   %left %1
MUL := @EXPR:lhs '*' @EXPR:rhs   %left %2
DIV := @EXPR:lhs '/' @EXPR:rhs   %left %2

PAREN := '(' @EXPR ')'
```

The parser uses these annotations to resolve shift-reduce conflicts during table construction. Terminals inherit precedence/associativity from the annotated productions that use them; assigning the same terminal conflicting levels is a grammar error. A production without an explicit `%N` falls back to its last terminal's level (yacc convention).

## Transforms and Reduce Functions

Grammar extensions become usable in scripts via reduce functions. These are defined in grammar files using syntax similar to script functions but restricted to declarative definitions only (no imperative logic, no external dependencies).

When the parser reduces to a grammar atom that has a transform, it performs node substitution directly on the parser stack. The output node replaces the input. Transforms execute bottom-up, so nested structures transform inner nodes first.

Reduce functions are invoked in scripts using `name!(...)` syntax, similar to Rust macros. The `!` signals that special grammar is in use and tells the reader exactly which grammar file to consult.

Reduce functions return AST nodes, not language-level values. This is similar to Nim's macro system where macros operate on and produce AST representations. The reduce function constructs nodes using AST constructors provided by the compiler.

Example grammar file:
```
XML_TAG := <@IDENT:tag>@BODY</@IDENT>

html :: (XML_TAG) => 
    AstNew(
        AstIdent("HtmlDOM"),
        AstRecord([
            AstField("roots", AstArray([
                AstNew(
                    AstIdent("HtmlNode" ++ @tag),
                    AstRecord([
                        AstField("children", @BODY)
                    ])
                )
            ]))
        ])
    )
```

This produces AST equivalent to: `new HtmlDOM { roots = [ new HtmlDivNode { children = ... } ] }`

The `Ast*` constructors build typed AST nodes that the compiler understands. Captured values like `@tag` and `@BODY` are already AST nodes and can be spliced directly into the output tree. String concatenation (`++`) on identifiers enables dynamic type construction like `HtmlDivNode` from `div`.

Example script usage:
```
content := html!(<div>Hello World</div>)
```

Nesting is supported:
```
page := html!(<div>@{ sql!(SELECT title FROM pages) }</div>)
```

Because reduce functions operate at the AST level, they can perform arbitrary tree transformations including validation, desugaring, and code generation.

## Parser

The compiler uses an LALR(1) parser. This provides fast O(n) parsing with compact tables and good cache locality. LALR is battle-tested in tools like yacc and bison.

Tables are built by canonical LR(1) construction followed by merging states with identical cores — simple and exactly correct at the grammar sizes involved. Reduce-reduce conflicts introduced by merging are reported, never silently resolved. EBNF sugar (`?`, `*`, `+`, groups) desugars into synthetic left-recursive productions (`ATOM#repN`, `ATOM#optN`) whose semantic actions thread list/optional values back to the parent node's fields.

Grammar validation occurs at three levels. Before table generation: check for undefined symbols, unreachable rules, unused terminals. During table construction: detect shift-reduce and reduce-reduce conflicts, verify completeness and reachability. At runtime: unit tests verify correct parsing of known inputs.

Precedence annotations (`%N`) resolve conflicts during table construction. Left recursion is supported natively.

## Error Handling

Errors accumulate rather than failing fast, allowing multiple issues to surface in a single compile. Error messages follow Rust's style with clear context and source locations.

Every token and AST node carries inline source spans: start line, start column, end line, end column. This enables precise error reporting.

## Dependency Resolution

Build files declare dependencies with `using PackageName v1.0`. The compiler parses build files, extracts dependencies, loads them recursively, and collects all grammar files.

Each package's grammar rules exist under an implicit namespace. When merged into the global grammar table for LR table construction, namespacing prevents conflicts. Grammar load order is irrelevant; the result is a set union of all productions.

## Visibility and Namespacing

Everything is public. There are no private, protected, or visibility keywords.

Namespaces are implicit based on package name and folder structure. No manual namespace declarations.

## Compilation Pipeline

```
                         ┌─────────────────────────────────────────────────────────────┐
                         │                     PHASE 1: BOOTSTRAP                      │
                         └─────────────────────────────────────────────────────────────┘
                                                      │
                                                      ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│   Parse args     │───▶│  Detect project  │───▶│  Load core       │
│   & validate     │    │  root directory  │    │  grammar files   │
└──────────────────┘    └──────────────────┘    └──────────────────┘
                                                      │
                         ┌─────────────────────────────────────────────────────────────┐
                         │                 PHASE 2: GRAMMAR COLLECTION                 │
                         └─────────────────────────────────────────────────────────────┘
                                                      │
                                                      ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│  Find project    │───▶│  Build LALR(1)   │───▶│  Construct build │
│  .build.grammar  │    │  table for build │    │  grammar atoms   │
│  files           │    │  file parsing    │    │  table           │
└──────────────────┘    └──────────────────┘    └──────────────────┘
                                                      │
                         ┌─────────────────────────────────────────────────────────────┐
                         │               PHASE 3: DEPENDENCY RESOLUTION                │
                         └─────────────────────────────────────────────────────────────┘
                                                      │
                                                      ▼
                         ┌──────────────────┐
                         │  Find & parse    │◀─────────────────────┐
                         │  .build files    │                      │
                         └────────┬─────────┘                      │
                                  │                                │
                                  ▼                                │
                         ┌──────────────────┐                      │
                         │  Extract `using` │                      │
                         │  declarations    │                      │
                         └────────┬─────────┘                      │
                                  │                                │
                                  ▼                                │
                         ┌──────────────────┐    ┌──────────────────┐
                         │  Load dependency │───▶│  Collect dep's   │
                         │  packages        │    │  grammar files   │──┘
                         └──────────────────┘    └──────────────────┘
                                                 (repeat until all
                                                  deps resolved)
                                                      │
                         ┌─────────────────────────────────────────────────────────────┐
                         │                PHASE 4: SCRIPT COMPILATION                  │
                         └─────────────────────────────────────────────────────────────┘
                                                      │
                                                      ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│  Merge all       │───▶│  Build LALR(1)   │───▶│  Find & parse    │
│  .grammar and    │    │  table for       │    │  .script files   │
│  .script.grammar │    │  script parsing  │    │                  │
└──────────────────┘    └──────────────────┘    └──────────────────┘
                                                      │
                                                      ▼
                         ┌──────────────────┐    ┌──────────────────┐
                         │  Apply reduce    │───▶│  Generate IR     │
                         │  transforms      │    │  from AST        │
                         └──────────────────┘    └──────────────────┘
                                                      │
                         ┌─────────────────────────────────────────────────────────────┐
                         │                   PHASE 5: BUILD EXECUTION                  │
                         └─────────────────────────────────────────────────────────────┘
                                                      │
                                                      ▼
                         ┌──────────────────┐    ┌──────────────────┐
                         │  Run build file  │───▶│  Check exit code │
                         │  Main() in VM    │    │  (0 = continue)  │
                         └──────────────────┘    └──────────────────┘
                                                      │
                         ┌─────────────────────────────────────────────────────────────┐
                         │                   PHASE 6: CODE GENERATION                  │
                         └─────────────────────────────────────────────────────────────┘
                                                      │
                                                      ▼
                         ┌──────────────────┐
                         │  Select backend  │
                         └────────┬─────────┘
                                  │
                    ┌─────────────┴─────────────┐
                    ▼                           ▼
           ┌──────────────────┐       ┌──────────────────┐
           │  VM Backend      │       │  Assembly Backend│
           │  ─────────────   │       │  ───────────────│
           │  Emit bytecode   │       │  Emit native asm│
           │  Execute in VM   │       │  Link & output  │
           └──────────────────┘       └──────────────────┘
```

**Phase Summary**

| Phase | Input | Output | Notes |
|-------|-------|--------|-------|
| 1. Bootstrap | CLI args | Project root, core grammar | Validates args, locates project |
| 2. Grammar Collection | Project `.build.grammar` files | Build parser LALR table | Core + project build grammars only |
| 3. Dependency Resolution | `.build` files | Complete dependency graph, all grammar files | Recursive until fixpoint |
| 4. Script Compilation | `.script` files, merged grammar | IR | Excludes `.build.grammar` from script parser |
| 5. Build Execution | Build file AST | Exit code | Non-zero aborts compilation |
| 6. Code Generation | IR | Bytecode or native binary | Backend selected by build config |

## Build Profiles

Projects can have multiple build files for different profiles: `develop.build`, `staging.build`, `prod.build`. The build command accepts profile names or defaults to a single profile if only one exists.

VM backend supports only one profile per build. Assembly backend can produce multiple outputs.

## Caching

Deferred for the vertical slice. Future work includes cached LR tables keyed by grammar content hash and precompiled library binaries to skip re-parsing dependencies.

## Vertical Slice

**Status: implemented.** `build <project-path> [profiles...] --backend <vm|asm>`.

Supported features: integer literals, float literals, binary operators (`+`, `-`, `*`, `/`), variable binding with `:=` (re-binding allocates a fresh slot, Rust let-style shadowing), and a `print` builtin. Mixed int/float arithmetic promotes to float; `int / int` truncates toward zero.

Not included in vertical slice: functions, control flow, user-defined types, dependencies (`using` reports "not yet supported"), reduce functions/transforms, multiple script files.

The slice validates the full pipeline: grammar files (`Core/core.build.grammar`, `Core/core.script.grammar`) → grammar meta-parser → EBNF desugaring → LALR(1) table construction → grammar-driven lexer → table-driven parser → typed IR → both backends. `Examples/Arithmetic` is the reference project; both backends print identical output for it.

**Backends.** The VM backend emits bytecode (pooled constants, u16 operands) and executes immediately in a stack-machine interpreter. The asm backend emits a MASM-style listing plus a runnable PE32+ executable at `out/<package>.asm|.exe`, using the compiler's **own toolchain**: a hand-written x64 encoder (fixed-size encodings over a closed instruction set) and a hand-written PE32+ linker (.text/.rdata/.idata, imports `msvcrt!printf` + `kernel32!ExitProcess`, entry point is generated code — msvcrt self-initializes, no CRT startup, no external assembler or linker, no ASLR so only rip-relative fixups exist). Codegen is stack-machine style on the hardware stack; operand depth is statically known, so printf call sites reserve 32/40 bytes to keep RSP 16-aligned, and Win64 varargs floats are duplicated in both `xmm1` and `rdx`.

**Print parity.** The asm backend prints via `printf("%lld\n")` / `printf("%g\n")`; the VM emulates those formats exactly (invariant int formatting; G6 with printf-style exponent rendering). Known divergences, accepted for the slice: msvcrt renders inf/NaN as `1.#INF`-style; line endings differ (msvcrt text mode emits CRLF, the VM emits LF); integer division by zero is a clean runtime error in the VM but a hardware #DE in the exe.
