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

Bare text represents literals: `using`, `v`, `(`.

Grouping uses parentheses. `?` marks optional, `*` marks zero or more, `+` marks one or more. Alternation uses `|` inline or as separate rules for the same atom.

Precedence uses annotations rather than stratified grammar. The `%N` suffix declares precedence level where higher numbers bind tighter. Associativity is declared with `%left`, `%right`, or `%none`.

Example:
```
EXPR := @EXPR $PLUS @EXPR   %left %1
EXPR := @EXPR $MINUS @EXPR  %left %1
EXPR := @EXPR $TIMES @EXPR  %left %2
EXPR := @EXPR $DIV @EXPR    %left %2
EXPR := @NUMBER
EXPR := $LPAREN @EXPR $RPAREN
```

The parser uses these annotations to resolve shift-reduce conflicts during table construction.

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

The initial implementation targets a minimal language to exercise the full pipeline.

Supported features: integer literals, float literals, binary operators (`+`, `-`, `*`, `/`), variable binding with `:=`, and a `print` builtin.

Not included in vertical slice: functions, control flow, user-defined types.

This scope is sufficient to validate: lexer, LALR parser, AST construction, IR generation, bytecode emission, and VM execution.
