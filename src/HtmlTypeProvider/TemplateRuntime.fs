module HtmlTypeProvider.TemplateRuntime

open System.Text.RegularExpressions
open HtmlTypeProvider.TemplatingInternals

// `${Word}` is a hole; `$$` in a run of dollars ending at `{` is an escape
// that collapses to a literal `$` (so `$${name}` renders as literal `${name}`).
// Keep in sync with the compile-time tokenizer in HtmlTypeProvider.Provider/Parsing.fs.
let private tokenRE = Regex(@"\$\$(?=\$*\{)|\$\{(\w+)\}", RegexOptions.Compiled)

let private isEscape (m: Match) = m.Value = "$$"

/// Extract unique hole names from a template string, in order of first occurrence.
/// Escaped holes ($${name}) are not counted.
let ExtractHoleNames (template: string) : string[] =
    let seen = System.Collections.Generic.HashSet<string>()
    let result = ResizeArray<string>()
    for m in tokenRE.Matches(template) |> Seq.cast<Match> do
        if not (isEscape m) then
            let name = m.Groups.[1].Value
            if seen.Add(name) then
                result.Add(name)
    result.ToArray()

/// Validate that the runtime template has exactly the same holes as the compile-time template.
/// Returns null if valid, or an error message string if not.
let ValidateHoles (expectedHoles: string[]) (runtimeTemplate: string) : string =
    let runtimeHoles = ExtractHoleNames runtimeTemplate
    let expected = System.Collections.Generic.HashSet<string>(expectedHoles)
    let actual = System.Collections.Generic.HashSet<string>(runtimeHoles)
    if expected.SetEquals(actual) then null
    else
        let missing = expected |> Seq.filter (fun h -> not (actual.Contains h)) |> Seq.toArray
        let extra = actual |> Seq.filter (fun h -> not (expected.Contains h)) |> Seq.toArray
        let parts = ResizeArray<string>()
        if missing.Length > 0 then
            parts.Add(sprintf "Missing holes: %s" (System.String.Join(", ", missing)))
        if extra.Length > 0 then
            parts.Add(sprintf "Unexpected holes: %s" (System.String.Join(", ", extra)))
        System.String.Join("; ", parts)

/// Validate and set the runtime override on a TemplateNode.
/// Throws ArgumentException if holes don't match.
let InitWithOverride (node: TemplateNode) (holeNames: string[]) (runtimeTemplate: string) =
    let error = ValidateHoles holeNames runtimeTemplate
    if not (isNull error) then
        invalidArg "runtimeTemplate"
            (sprintf "Runtime template holes do not match compile-time template. %s" error)
    node.HoleNames <- holeNames
    node.RuntimeTemplate <- runtimeTemplate

/// Render a rawText template by substituting ${Hole} placeholders with hole values
/// and collapsing $$ escapes to a literal $.
let RenderRawText (template: string) (holeNames: string[]) (holes: obj[]) : string =
    let holeMap = System.Collections.Generic.Dictionary<string, int>()
    for i = 0 to holeNames.Length - 1 do
        holeMap.[holeNames.[i]] <- i
    tokenRE.Replace(template, MatchEvaluator(fun m ->
        if isEscape m then "$" else
        let name = m.Groups.[1].Value
        match holeMap.TryGetValue(name) with
        | true, idx -> string holes.[idx]
        | _ -> m.Value))
