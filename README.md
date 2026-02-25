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

## Parameters

```fsharp
type T = HtmlTypeProvider.Template<
    pathOrHtml: string,           // File path or inline HTML string
    optimizePlainHtml: bool       // Default: true. Collapse hole-free HTML segments
>
```

## Safety

- All text content holes are HTML-encoded via `System.Net.WebUtility.HtmlEncode`
- All attribute values are HTML-encoded
- `Node.RawHtml` and `PlainHtml` from template source pass through unencoded
- Void elements (`br`, `img`, `input`, etc.) render as self-closing HTML5

## License

Apache 2.0
