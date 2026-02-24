namespace HtmlTypeProvider

open System.Net
open System.Text

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Attr =

    let Make (name: string) (value: obj) =
        Attr(fun sb ->
            sb.Append(' ').Append(name).Append("=\"") |> ignore
            sb.Append(WebUtility.HtmlEncode(string value)).Append('"') |> ignore)

    let Attrs (attrs: seq<Attr>) =
        Attr(fun sb ->
            for attr in attrs do
                attr.Invoke(sb))

    let Empty () =
        Attr(fun _ -> ())
