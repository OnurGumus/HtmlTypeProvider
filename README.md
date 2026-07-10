# HtmlTypeProvider

An F# type provider that parses HTML templates with `${HoleName}` holes and generates strongly-typed builder APIs that produce HTML strings.

Inspired by [Bolero](https://github.com/fsbolero/Bolero)'s templating system, but completely independent of Blazor/WebAssembly. Output is plain `string` via `StringBuilder` — no runtime framework dependencies.

## Installation

```
dotnet add package HtmlTypeProvider
```

## Quick Start

### Inline HTML

```fsharp
type Greeting = HtmlTypeProvider.Template<"<h1>Hello, ${Name}!</h1>">

let html = Greeting().Name("World").Render()
// "<h1>Hello, World!</h1>"
```

### File-based templates

```html
<!-- templates/page.html -->
<html>
<head><title>${Title}</title></head>
<body>
  <h1>${Title}</h1>
  <div class="${BodyClass}">${Content}</div>
  <template id="Card">
    <div class="card">
      <h2>${CardTitle}</h2>
      <p>${CardBody}</p>
    </div>
  </template>
</body>
</html>
```

```fsharp
type Page = HtmlTypeProvider.Template<"templates/page.html">

let html =
    Page()
        .Title("My Site")
        .BodyClass("container")
        .Content(
            Node.Fragment [|
                Page.Card().CardTitle("Card 1").CardBody("Hello").Elt()
                Page.Card().CardTitle("Card 2").CardBody("World").Elt()
            |])
        .Render()
```

## Hole Types

Holes are detected by their position in the template:

| Position | Example | Generated Method | Notes |
|----------|---------|-----------------|-------|
| Text content | `<p>${X}</p>` | `.X(value: string)` / `.X(value: Node)` | HTML-encoded string or composable Node |
| Single attribute value | `<div class="${X}">` | `.X(value: string)` / `.X(value: int)` / ... | Typed overloads: string, int, float, bool, obj |
| Mixed attribute | `<div class="a-${X}">` | `.X(value: string)` | String interpolation in attribute |
| Full attribute | `<div attr="${X}">` | `.X(value: Attr)` / `.X(value: Attr list)` | Dynamic attribute(s) |

The same hole name can appear multiple times — types are merged automatically.

## Escaping `$` (JavaScript, shell scripts, etc.)

To output a literal `${...}` — for example a JavaScript template literal inside a `<script>` tag — escape the dollar by doubling it: `$${` renders as `${` and is not treated as a hole.

```html
<script>
  const greeting = `Hello $${user.name}, you have $${count} messages`;
</script>
```

renders as:

```html
<script>
  const greeting = `Hello ${user.name}, you have ${count} messages`;
</script>
```

The rules, applied left to right:

| Template | Output | Notes |
|----------|--------|-------|
| `${Name}` | hole value | A hole, as usual |
| `$${Name}` | `${Name}` | Escaped — literal text, no hole |
| `$$${Name}` | `$` + hole value | Literal `$` followed by a real hole |
| `$${any text}` | `${any text}` | Escape also silences the malformed-hole error |
| `$$` (not before `{`) | `$$` | Dollars away from `{` never need escaping |

In short: in a run of dollars immediately preceding `{`, each `$$` pair collapses to one literal `$`; if one `$` remains, it starts a hole. Escaping works everywhere holes do: text content, attribute values, `rawText` templates, and runtime template overrides.

## API Reference

### Builder Methods

Every template type has:
- **Constructor** `MyTemplate()` — creates a new builder with default (empty) hole values
- **Hole setters** `.HoleName(value)` — fluent methods, return the builder for chaining
- **`.Elt()`** — returns a `Node` for composition with other templates
- **`.Render()`** — returns the final HTML `string`

### Node Module

```fsharp
Node.Empty()                            // No output
Node.Text "safe text"                   // HTML-encoded text
Node.RawHtml "<b>raw</b>"              // Unencoded passthrough
Node.Elt "div" [| attrs |] [| kids |]  // Element with attrs and children
Node.Fragment [| node1; node2 |]         // Sibling nodes without wrapper
Node.Render node                        // Node -> string
```

### Attr Module

```fsharp
Attr.Make "name" (box "value")   // name="encoded-value"
Attr.Flag "disabled"             // Boolean attribute (no value)
Attr.Attrs [| attr1; attr2 |]   // Group multiple attrs
Attr.Empty()                     // No output
```

## Nested Templates

Use `<template id="Name">` inside your HTML to define sub-templates. They become nested types:

```fsharp
type Page = HtmlTypeProvider.Template<"page.html">

// Access nested template as Page.Card
let card = Page.Card().CardTitle("Hi").Elt()
```

## Raw Text Templates

With `rawText=true` the template is treated as plain text instead of HTML: no HTML parsing, no encoding of hole values, no nested `<template>` support. This is useful for LLM prompts, emails, config files, or any non-HTML text with `${Hole}` placeholders:

`templates/greeting.txt`:

```text
Hello ${Name}, welcome to ${Place}!
```

```fsharp
type Greeting = HtmlTypeProvider.Template<"templates/greeting.txt", rawText=true>

let text = Greeting().Name("Alice").Place("Wonderland").Render()
// "Hello Alice, welcome to Wonderland!"
```

Inline raw text works too — a string that doesn't resolve to an existing file is used as the template itself:

```fsharp
type Prompt = HtmlTypeProvider.Template<"You are a ${Role} at ${Company}.", rawText=true>
```

Note: raw text hole values are substituted verbatim (not HTML-encoded). `.Elt()` wraps the result in `Node.RawHtml`, so only compose it into HTML if the content is trusted.

## Runtime Template Overrides

Raw text templates get a second constructor that accepts a replacement template **at runtime**, while keeping the compile-time typed API. This lets you load edited templates from a database or config without recompiling:

```fsharp
type Greeting = HtmlTypeProvider.Template<"templates/greeting.txt", rawText=true>

// Compile-time template
Greeting().Name("A").Place("B").Render()
// "Hello A, welcome to B!"

// Runtime override — same holes, different text
let fromDb = "Greetings ${Name}! You are in ${Place}."
Greeting(fromDb).Name("A").Place("B").Render()
// "Greetings A! You are in B."
```

The override is validated on construction: it must contain **exactly the same holes** as the compile-time template — no missing holes, no extras — otherwise the constructor throws `ArgumentException` listing the mismatch. Escaped holes (`$${...}`) in the override render literally and don't count toward validation.

## Parameters

```fsharp
type T = HtmlTypeProvider.Template<
    pathOrHtml: string,           // File path or inline template string
    optimizePlainHtml: bool,      // Default: true. Collapse hole-free HTML segments
    rawText: bool                 // Default: false. Treat template as plain text (no HTML parsing/encoding)
>
```

## Troubleshooting

### High CPU usage in IDE with .NET 10 / F# 10

If you experience sustained high CPU from `fsautocomplete` (the F# language server) when using this provider with F# 10, it is caused by an [FCS bug](https://github.com/dotnet/fsharp/pull/19369) in the type subsumption cache where `TypeStructure.GetHashCode` performs O(n) hashing on every cache lookup.

**Workaround:** Set the environment variable `FSharp_CacheEvictionImmediate=1` before launching your editor. This switches the cache eviction strategy from a background worker (which continuously rehashes) to synchronous inline eviction, eliminating the CPU spike while keeping the cache functional.

On macOS (persists until reboot):
```bash
launchctl setenv FSharp_CacheEvictionImmediate 1
```

On Linux/Windows, set the variable in your shell profile or system environment variables.

After setting it, fully restart your editor (not just reload).

## Safety

- All text content holes are HTML-encoded via `System.Net.WebUtility.HtmlEncode`
- All attribute values are HTML-encoded
- `Node.RawHtml` and `PlainHtml` from template source pass through unencoded
- Void elements (`br`, `img`, `input`, etc.) render as self-closing HTML5

## License

Apache 2.0
