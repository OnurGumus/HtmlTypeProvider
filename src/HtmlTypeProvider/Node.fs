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

    let Empty () =
        Node(fun _ -> ())

    let Elt (name: string) (attrs: seq<Attr>) (children: seq<Node>) =
        Node(fun sb ->
            sb.Append('<').Append(name) |> ignore
            for attr in attrs do
                attr.Invoke(sb)
            if voidElements.Contains(name) then
                sb.Append(" />") |> ignore
            else
                sb.Append('>') |> ignore
                for child in children do
                    child.Invoke(sb)
                sb.Append("</").Append(name).Append('>') |> ignore)

    let Text (text: string) =
        Node(fun sb ->
            sb.Append(WebUtility.HtmlEncode(text)) |> ignore)

    let RawHtml (html: string) =
        Node(fun sb ->
            sb.Append(html) |> ignore)

    let Concat (nodes: seq<Node>) =
        Node(fun sb ->
            for node in nodes do
                node.Invoke(sb))

    let Render (node: Node) : string =
        let sb = StringBuilder()
        node.Invoke(sb)
        sb.ToString()
