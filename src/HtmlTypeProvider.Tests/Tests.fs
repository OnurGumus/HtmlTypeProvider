module HtmlTypeProvider.Tests

open Expecto
open HtmlTypeProvider

// ============================================================
// Type aliases (must be at module level for type providers)
// ============================================================

type InlineSimple = HtmlTypeProvider.Template<"<div>${Content}</div>">
type InlineAttr = HtmlTypeProvider.Template<"""<div class="${Cls}">text</div>""">
type InlineMixed = HtmlTypeProvider.Template<"""<div class="prefix-${Cls}">text</div>""">
type InlineMultiAttrHole = HtmlTypeProvider.Template<"""<div class="${A}-${B}">text</div>""">
type InlineMultiHole = HtmlTypeProvider.Template<"""<h1>${Title}</h1><p>${Title}</p>""">
type InlineHtmlHole = HtmlTypeProvider.Template<"<div>${Inner}</div>">
type InlineNoHoles = HtmlTypeProvider.Template<"<p>static</p>">
type InlineAttrHole = HtmlTypeProvider.Template<"""<div attr="${DynAttrs}">content</div>""">
type InlineCrossCtx = HtmlTypeProvider.Template<"""<div title="${X}">${X}</div>""">
type InlineOptimized = HtmlTypeProvider.Template<"<div>${Content}</div>", optimizePlainHtml=true>
type InlineOptimizedStatic = HtmlTypeProvider.Template<"""<header><nav><a href="/">Home</a></nav></header><main>${Body}</main>""", optimizePlainHtml=true>
type BasicFile = HtmlTypeProvider.Template<"templates/basic.html">
type VoidHoles = HtmlTypeProvider.Template<"templates/void-holes.html">
type AttrHoleFile = HtmlTypeProvider.Template<"templates/attr-hole.html">
type NestedFile = HtmlTypeProvider.Template<"templates/nested.html">
type MultiNested = HtmlTypeProvider.Template<"templates/multi-nested.html">

// ============================================================
// Runtime unit tests
// ============================================================

let runtimeTests = testList "Runtime" [
    testCase "Node.Text encodes HTML entities" <| fun () ->
        let result = Node.Render(Node.Text "<b>hi</b>")
        Expect.equal result "&lt;b&gt;hi&lt;/b&gt;" ""

    testCase "Node.RawHtml passes through" <| fun () ->
        let result = Node.Render(Node.RawHtml "<b>hi</b>")
        Expect.equal result "<b>hi</b>" ""

    testCase "Node.Empty produces nothing" <| fun () ->
        let result = Node.Render(Node.Empty())
        Expect.equal result "" ""

    testCase "Node.Text with empty string" <| fun () ->
        let result = Node.Render(Node.Text "")
        Expect.equal result "" ""

    testCase "Node.Elt renders element with attrs and children" <| fun () ->
        let node = Node.Elt "div" [| Attr.Make "class" (box "foo") |] [| Node.Text "bar" |]
        let result = Node.Render node
        Expect.equal result """<div class="foo">bar</div>""" ""

    testCase "Node.Elt renders void element as HTML5" <| fun () ->
        let node = Node.Elt "br" [||] [||]
        let result = Node.Render node
        Expect.equal result "<br>" ""

    testCase "Node.Elt renders img void element with attrs" <| fun () ->
        let node = Node.Elt "img" [| Attr.Make "src" (box "a.png") |] [||]
        let result = Node.Render node
        Expect.equal result """<img src="a.png">""" ""

    testCase "Node.Elt void element is case-insensitive" <| fun () ->
        let node = Node.Elt "BR" [||] [||]
        let result = Node.Render node
        Expect.equal result "<BR>" ""

    testCase "Node.Fragment joins multiple nodes" <| fun () ->
        let node = Node.Fragment [| Node.Text "a"; Node.Text "b" |]
        let result = Node.Render node
        Expect.equal result "ab" ""

    testCase "Node.Fragment with empty sequence" <| fun () ->
        let result = Node.Render(Node.Fragment [||])
        Expect.equal result "" ""

    testCase "Attr.Empty produces nothing" <| fun () ->
        let node = Node.Elt "div" [| Attr.Empty() |] [||]
        let result = Node.Render node
        Expect.equal result "<div></div>" ""

    testCase "Attr.Attrs groups attributes" <| fun () ->
        let attrs = Attr.Attrs [| Attr.Make "a" (box "1"); Attr.Make "b" (box "2") |]
        let node = Node.Elt "div" [| attrs |] [||]
        let result = Node.Render node
        Expect.equal result """<div a="1" b="2"></div>""" ""

    testCase "Attr.Make encodes attribute values" <| fun () ->
        let node = Node.Elt "div" [| Attr.Make "title" (box """a"b""") |] [||]
        let result = Node.Render node
        Expect.equal result """<div title="a&quot;b"></div>""" ""

    testCase "Attr.Flag renders boolean attribute without value" <| fun () ->
        let node = Node.Elt "input" [| Attr.Flag "disabled" |] [||]
        let result = Node.Render node
        Expect.equal result "<input disabled>" ""

    testCase "Attr.Flag combined with Make" <| fun () ->
        let node = Node.Elt "input" [| Attr.Make "type" (box "text"); Attr.Flag "readonly" |] [||]
        let result = Node.Render node
        Expect.equal result """<input type="text" readonly>""" ""

    testCase "Attr.Make uses InvariantCulture for floats" <| fun () ->
        let node = Node.Elt "div" [| Attr.Make "data-val" (box 1.5) |] [||]
        let result = Node.Render node
        Expect.equal result """<div data-val="1.5"></div>""" ""

    testCase "Attr.Make with integer value" <| fun () ->
        let node = Node.Elt "div" [| Attr.Make "tabindex" (box 3) |] [||]
        let result = Node.Render node
        Expect.equal result """<div tabindex="3"></div>""" ""

    testCase "Attr.Make with boolean value" <| fun () ->
        let node = Node.Elt "div" [| Attr.Make "data-active" (box true) |] [||]
        let result = Node.Render node
        Expect.equal result """<div data-active="True"></div>""" ""

    testCase "Node.Text encodes XSS attempt" <| fun () ->
        let result = Node.Render(Node.Text """<script>alert('xss')</script>""")
        Expect.equal result "&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;" ""

    testCase "Node.Render with deeply nested elements" <| fun () ->
        let inner = Node.Elt "span" [||] [| Node.Text "deep" |]
        let mid = Node.Elt "p" [||] [| inner |]
        let outer = Node.Elt "div" [||] [| mid |]
        let result = Node.Render outer
        Expect.equal result "<div><p><span>deep</span></p></div>" ""
]

// ============================================================
// Type Provider tests: inline HTML
// ============================================================

let inlineTests = testList "Inline" [
    testCase "Inline template with Html hole renders exact output" <| fun () ->
        let result = InlineSimple().Content("hello").Render()
        Expect.equal result "<div>hello</div>" ""

    testCase "Inline template Elt returns composable Node" <| fun () ->
        let node = InlineSimple().Content("inner").Elt()
        let wrapper = Node.Elt "section" [||] [| node |]
        let result = Node.Render wrapper
        Expect.equal result "<section><div>inner</div></section>" ""

    testCase "Inline template with AttributeValue hole exact output" <| fun () ->
        let result = InlineAttr().Cls("my-class").Render()
        Expect.equal result """<div class="my-class">text</div>""" ""

    testCase "Inline template with mixed attr text exact output" <| fun () ->
        let result = InlineMixed().Cls("suffix").Render()
        Expect.equal result """<div class="prefix-suffix">text</div>""" ""

    testCase "Inline template with multiple holes in one attribute" <| fun () ->
        let result = InlineMultiAttrHole().A("foo").B("bar").Render()
        Expect.equal result """<div class="foo-bar">text</div>""" ""

    testCase "Inline template reuses same hole" <| fun () ->
        let result = InlineMultiHole().Title("Hi").Render()
        Expect.equal result "<h1>Hi</h1><p>Hi</p>" ""

    testCase "Inline template with Node value" <| fun () ->
        let inner = Node.Elt "b" [||] [| Node.Text "bold" |]
        let result = InlineHtmlHole().Inner(inner).Render()
        Expect.equal result "<div><b>bold</b></div>" ""

    testCase "Inline template Html hole with string encodes XSS" <| fun () ->
        let result = InlineSimple().Content("<script>alert(1)</script>").Render()
        Expect.equal result "<div>&lt;script&gt;alert(1)&lt;/script&gt;</div>" ""

    testCase "Inline template with no holes renders static content" <| fun () ->
        let result = InlineNoHoles().Render()
        Expect.equal result "<p>static</p>" ""

    testCase "Inline template with no holes Elt returns Node" <| fun () ->
        let node = InlineNoHoles().Elt()
        let result = Node.Render node
        Expect.equal result "<p>static</p>" ""

    testCase "Inline template with full Attr hole single attr" <| fun () ->
        let result = InlineAttrHole().DynAttrs(Attr.Make "class" (box "x")).Render()
        Expect.equal result """<div class="x">content</div>""" ""

    testCase "Inline template with full Attr hole multiple attrs" <| fun () ->
        let attrs = [Attr.Make "id" (box "myid"); Attr.Make "class" (box "cls")]
        let result = InlineAttrHole().DynAttrs(attrs).Render()
        Expect.equal result """<div id="myid" class="cls">content</div>""" ""

    testCase "Inline template with full Attr hole empty" <| fun () ->
        let result = InlineAttrHole().DynAttrs(Attr.Empty()).Render()
        Expect.equal result "<div>content</div>" ""

    testCase "Cross-context hole merges to String" <| fun () ->
        let result = InlineCrossCtx().X("val").Render()
        Expect.stringContains result "val</div>" ""
        Expect.stringContains result """title="val""" ""

    testCase "Optimized template produces same output" <| fun () ->
        let result = InlineOptimized().Content("hello").Render()
        Expect.equal result "<div>hello</div>" ""

    testCase "Optimized template with static and dynamic parts" <| fun () ->
        let result = InlineOptimizedStatic().Body("content").Render()
        Expect.stringContains result """<a href="/">Home</a>""" ""
        Expect.stringContains result "content</main>" ""

    testCase "Setting hole twice uses last value" <| fun () ->
        let result = InlineSimple().Content("first").Content("second").Render()
        Expect.equal result "<div>second</div>" ""

    testCase "Render can be called multiple times" <| fun () ->
        let tpl = InlineSimple().Content("x")
        let r1 = tpl.Render()
        let r2 = tpl.Render()
        Expect.equal r1 r2 ""

    testCase "AttributeValue hole accepts int" <| fun () ->
        let result = InlineAttr().Cls(42).Render()
        Expect.equal result """<div class="42">text</div>""" ""

    testCase "AttributeValue hole accepts float" <| fun () ->
        let result = InlineAttr().Cls(3.14).Render()
        Expect.equal result """<div class="3.14">text</div>""" ""

    testCase "AttributeValue hole accepts bool" <| fun () ->
        let result = InlineAttr().Cls(true).Render()
        Expect.equal result """<div class="True">text</div>""" ""
]

// ============================================================
// Type Provider tests: file-based
// ============================================================

let fileBasedTests = testList "File-based" [
    testCase "File template with multiple holes" <| fun () ->
        let result =
            BasicFile()
                .Title("My Page")
                .BodyClass("container")
                .Content("Main content")
                .Render()
        Expect.stringContains result "<title>My Page</title>" ""
        Expect.stringContains result "My Page</h1>" ""
        Expect.stringContains result "container" ""
        Expect.stringContains result "Main content" ""

    testCase "File template Elt returns Node" <| fun () ->
        let node =
            BasicFile()
                .Title("T")
                .BodyClass("c")
                .Content("X")
                .Elt()
        let result = Node.Render node
        Expect.stringContains result "<title>T</title>" ""

    testCase "Void elements with attribute holes" <| fun () ->
        let result =
            VoidHoles()
                .Val("hello")
                .ImgSrc("pic.jpg")
                .ImgAlt("a pic")
                .Render()
        Expect.stringContains result """value="hello""" ""
        Expect.stringContains result """src="pic.jpg""" ""
        Expect.stringContains result """alt="a pic""" ""
        Expect.isFalse (result.Contains("</input>")) "should not contain </input>"
        Expect.isFalse (result.Contains("</img>")) "should not contain </img>"

    testCase "File template with full Attr hole" <| fun () ->
        let result = AttrHoleFile().DynAttrs(Attr.Make "data-x" (box "1")).Render()
        Expect.stringContains result """data-x="1""" ""
        Expect.stringContains result "content" ""
]

// ============================================================
// Nested template tests
// ============================================================

let nestedTests = testList "Nested" [
    testCase "Nested template renders card" <| fun () ->
        let card = NestedFile.Card().CardTitle("Title1").CardBody("Body1").Elt()
        let result =
            NestedFile()
                .Heading("Hello")
                .Body(card)
                .Render()
        Expect.stringContains result "Hello</h1>" ""
        Expect.stringContains result "Title1</h2>" ""
        Expect.stringContains result "Body1</p>" ""

    testCase "Nested template multiple cards" <| fun () ->
        let cards = Node.Fragment [|
            NestedFile.Card().CardTitle("A").CardBody("1").Elt()
            NestedFile.Card().CardTitle("B").CardBody("2").Elt()
        |]
        let result =
            NestedFile()
                .Heading("Cards")
                .Body(cards)
                .Render()
        Expect.stringContains result "A</h2>" ""
        Expect.stringContains result "B</h2>" ""

    testCase "Nested template Render directly" <| fun () ->
        let result = NestedFile.Card().CardTitle("X").CardBody("Y").Render()
        Expect.stringContains result "X</h2>" ""
        Expect.stringContains result "Y</p>" ""
        Expect.stringContains result """class="card""" ""

    testCase "Multiple nested templates in one file" <| fun () ->
        let alert = MultiNested.Alert().Level("danger").AlertMsg("Fire!").Elt()
        let badge = MultiNested.Badge().BadgeText("new").Elt()
        let result =
            MultiNested()
                .Title(Node.Fragment [| alert; badge |])
                .Render()
        Expect.stringContains result "alert-danger" ""
        Expect.stringContains result "Fire!" ""
        Expect.stringContains result "badge" ""
        Expect.stringContains result "new" ""

    testCase "Multi-nested Alert renders independently" <| fun () ->
        let result = MultiNested.Alert().Level("info").AlertMsg("Note").Render()
        Expect.stringContains result "alert-info" ""
        Expect.stringContains result "Note" ""

    testCase "Multi-nested Badge renders independently" <| fun () ->
        let result = MultiNested.Badge().BadgeText("5").Render()
        Expect.stringContains result "5</span>" ""
]

[<Tests>]
let allTests = testList "All" [
    runtimeTests
    inlineTests
    fileBasedTests
    nestedTests
]

[<EntryPoint>]
let main args = runTestsInAssemblyWithCLIArgs [] args
