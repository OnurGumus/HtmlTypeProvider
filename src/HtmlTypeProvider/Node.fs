namespace HtmlTypeProvider

open System.Net
open System.Text

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Node =

    let private voidElements =
        System.Collections.Generic.HashSet<string>(
            [| "area"; "base"; "br"; "col"; "embed"; "hr"; "img"; "input";
               "link"; "meta"; "param"; "source"; "track"; "wbr" |],
            System.StringComparer.OrdinalIgnoreCase)

    /// <summary>Create an empty Node that produces no output.</summary>
    let Empty () =
        Node(fun _ -> ())

    /// <summary>Create an HTML element.</summary>
    /// <param name="name">The element tag name.</param>
    /// <param name="attrs">The element's attributes.</param>
    /// <param name="children">The element's child nodes. Ignored for void elements.</param>
    let Elt (name: string) (attrs: seq<Attr>) (children: seq<Node>) =
        Node(fun sb ->
            sb.Append('<').Append(name) |> ignore
            for attr in attrs do
                attr.Invoke(sb)
            if voidElements.Contains(name) then
                sb.Append('>') |> ignore
            else
                sb.Append('>') |> ignore
                for child in children do
                    child.Invoke(sb)
                sb.Append("</").Append(name).Append('>') |> ignore)

    /// <summary>Create an HTML text node. The text is HTML-encoded.</summary>
    /// <param name="text">The text content.</param>
    let Text (text: string) =
        Node(fun sb ->
            sb.Append(WebUtility.HtmlEncode(text)) |> ignore)

    /// <summary>Create a Node from raw HTML. The content is NOT encoded.</summary>
    /// <param name="html">The raw HTML string.</param>
    let RawHtml (html: string) =
        Node(fun sb ->
            sb.Append(html) |> ignore)

    /// <summary>Combine multiple nodes as siblings without a wrapper element.</summary>
    /// <param name="nodes">The sibling nodes.</param>
    let Fragment (nodes: seq<Node>) =
        Node(fun sb ->
            for node in nodes do
                node.Invoke(sb))

    /// <summary>Combine multiple nodes as siblings without a wrapper element.</summary>
    [<System.Obsolete("Use Node.Fragment instead.")>]
    let Concat (nodes: seq<Node>) = Fragment nodes

    /// <summary>Render a Node to a string.</summary>
    /// <param name="node">The node to render.</param>
    /// <returns>The rendered HTML string.</returns>
    let Render (node: Node) : string =
        let sb = StringBuilder()
        node.Invoke(sb)
        sb.ToString()
