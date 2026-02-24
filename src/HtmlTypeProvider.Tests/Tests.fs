module HtmlTypeProvider.Tests

open Xunit
open HtmlTypeProvider

// -- Runtime unit tests --

[<Fact>]
let ``Node.Text encodes HTML entities`` () =
    let result = Node.Render(Node.Text "<b>hi</b>")
    Assert.Equal("&lt;b&gt;hi&lt;/b&gt;", result)

[<Fact>]
let ``Node.RawHtml passes through`` () =
    let result = Node.Render(Node.RawHtml "<b>hi</b>")
    Assert.Equal("<b>hi</b>", result)

[<Fact>]
let ``Node.Empty produces nothing`` () =
    let result = Node.Render(Node.Empty())
    Assert.Equal("", result)

[<Fact>]
let ``Node.Elt renders element with attrs and children`` () =
    let node = Node.Elt "div" [| Attr.Make "class" (box "foo") |] [| Node.Text "bar" |]
    let result = Node.Render node
    Assert.Equal("""<div class="foo">bar</div>""", result)

[<Fact>]
let ``Node.Elt renders void element`` () =
    let node = Node.Elt "br" [||] [||]
    let result = Node.Render node
    Assert.Equal("<br />", result)

[<Fact>]
let ``Node.Elt renders img void element with attrs`` () =
    let node = Node.Elt "img" [| Attr.Make "src" (box "a.png") |] [||]
    let result = Node.Render node
    Assert.Equal("""<img src="a.png" />""", result)

[<Fact>]
let ``Node.Concat joins multiple nodes`` () =
    let node = Node.Concat [| Node.Text "a"; Node.Text "b" |]
    let result = Node.Render node
    Assert.Equal("ab", result)

[<Fact>]
let ``Attr.Empty produces nothing`` () =
    let node = Node.Elt "div" [| Attr.Empty() |] [||]
    let result = Node.Render node
    Assert.Equal("<div></div>", result)

[<Fact>]
let ``Attr.Attrs groups attributes`` () =
    let attrs = Attr.Attrs [| Attr.Make "a" (box "1"); Attr.Make "b" (box "2") |]
    let node = Node.Elt "div" [| attrs |] [||]
    let result = Node.Render node
    Assert.Equal("""<div a="1" b="2"></div>""", result)

[<Fact>]
let ``Attr.Make encodes attribute values`` () =
    let node = Node.Elt "div" [| Attr.Make "title" (box """a"b""") |] [||]
    let result = Node.Render node
    Assert.Equal("""<div title="a&quot;b"></div>""", result)

// -- Type Provider tests: inline HTML --

type InlineSimple = HtmlTypeProvider.Template<"<div>${Content}</div>">

[<Fact>]
let ``Inline template with Html hole`` () =
    let result = InlineSimple().Content("hello").Render()
    Assert.Contains("hello", result)

type InlineAttr = HtmlTypeProvider.Template<"""<div class="${Cls}">text</div>""">

[<Fact>]
let ``Inline template with AttributeValue hole`` () =
    let result = InlineAttr().Cls(box "my-class").Render()
    Assert.Contains("my-class", result)

type InlineMixed = HtmlTypeProvider.Template<"""<div class="prefix-${Cls}">text</div>""">

[<Fact>]
let ``Inline template with mixed attr text`` () =
    let result = InlineMixed().Cls("suffix").Render()
    Assert.Contains("prefix-suffix", result)

type InlineMultiHole = HtmlTypeProvider.Template<"""<h1>${Title}</h1><p>${Title}</p>""">

[<Fact>]
let ``Inline template reuses same hole`` () =
    let result = InlineMultiHole().Title("Hi").Render()
    Assert.Contains("<h1>Hi</h1>", result)
    Assert.Contains("<p>Hi</p>", result)

type InlineHtmlHole = HtmlTypeProvider.Template<"<div>${Inner}</div>">

[<Fact>]
let ``Inline template with Node value`` () =
    let inner = Node.Elt "b" [||] [| Node.Text "bold" |]
    let result = InlineHtmlHole().Inner(inner).Render()
    Assert.Contains("<b>bold</b>", result)

// -- Type Provider tests: file-based --

type BasicFile = HtmlTypeProvider.Template<"templates/basic.html">

[<Fact>]
let ``File template with multiple holes`` () =
    let result =
        BasicFile()
            .Title("My Page")
            .BodyClass(box "container")
            .Content("Main content")
            .Render()
    Assert.Contains("<title>My Page</title>", result)
    Assert.Contains("My Page</h1>", result)
    Assert.Contains("container", result)
    Assert.Contains("Main content", result)

// -- Nested template tests --

type NestedFile = HtmlTypeProvider.Template<"templates/nested.html">

[<Fact>]
let ``Nested template renders card`` () =
    let card = NestedFile.Card().CardTitle("Title1").CardBody("Body1").Elt()
    let result =
        NestedFile()
            .Heading("Hello")
            .Body(card)
            .Render()
    Assert.Contains("Hello</h1>", result)
    Assert.Contains("Title1</h2>", result)
    Assert.Contains("Body1</p>", result)

[<Fact>]
let ``Nested template multiple cards`` () =
    let cards = Node.Concat [|
        NestedFile.Card().CardTitle("A").CardBody("1").Elt()
        NestedFile.Card().CardTitle("B").CardBody("2").Elt()
    |]
    let result =
        NestedFile()
            .Heading("Cards")
            .Body(cards)
            .Render()
    Assert.Contains("A</h2>", result)
    Assert.Contains("B</h2>", result)
