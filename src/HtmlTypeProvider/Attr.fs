namespace HtmlTypeProvider

open System
open System.Globalization
open System.Net
open System.Text

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Attr =

    /// <summary>Create an HTML attribute with the given name and value.</summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The attribute value. Formatted with InvariantCulture and HTML-encoded.</param>
    let Make (name: string) (value: obj) =
        Attr(fun sb ->
            let valueStr =
                match value with
                | :? IFormattable as f -> f.ToString(null, CultureInfo.InvariantCulture)
                | v -> string v
            sb.Append(' ').Append(name).Append("=\"") |> ignore
            sb.Append(WebUtility.HtmlEncode(valueStr)).Append('"') |> ignore)

    /// <summary>Group multiple attributes into a single Attr value.</summary>
    /// <param name="attrs">The attributes to group.</param>
    let Attrs (attrs: seq<Attr>) =
        Attr(fun sb ->
            for attr in attrs do
                attr.Invoke(sb))

    /// <summary>Create a boolean HTML attribute (rendered without a value, e.g. <c>disabled</c>).</summary>
    /// <param name="name">The attribute name.</param>
    let Flag (name: string) =
        Attr(fun sb ->
            sb.Append(' ').Append(name) |> ignore)

    /// <summary>Create an empty Attr that produces no output.</summary>
    let Empty () =
        Attr(fun _ -> ())
