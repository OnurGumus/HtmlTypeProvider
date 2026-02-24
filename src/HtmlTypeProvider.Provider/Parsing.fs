module HtmlTypeProvider.Templating.Parsing

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open HtmlAgilityPack

type HoleType =
    | String
    | Html
    | Attribute
    | AttributeValue

module HoleType =

    let Merge (holeName: string) (t1: HoleType) (t2: HoleType) : HoleType =
        if t1 = t2 then t1 else
        match t1, t2 with
        | (String | Html | AttributeValue), (String | Html | AttributeValue) -> String
        | _ -> failwith $"Hole name used multiple times with incompatible types: {holeName}"

let HoleRE = Regex(@"\${(\w+)}", RegexOptions.Compiled)

type VarSubstitution =
    {
        name: string
        innerType: HoleType
        outerType: HoleType
    }

type Expr =
    | Concat of list<Expr>
    | PlainHtml of string
    | Elt of name: string * attrs: list<Expr> * children: list<Expr>
    | Attr of name: string * value: Expr
    | VarContent of varName: string
    | WrapVars of vars: list<VarSubstitution> * expr: Expr

type Vars = Map<string, HoleType>

module Vars =

    let Merge (vars1: Vars) (vars2: Vars) =
        (vars1, vars2) ||> Map.fold (fun map key type' ->
            let type' =
                match Map.tryFind key map with
                | None -> type'
                | Some type2 -> HoleType.Merge key type' type2
            Map.add key type' map
        )

    let MergeMany (vars: seq<Vars>) =
        Seq.fold Merge Map.empty vars

type Parsed =
    {
        Vars: Vars
        Expr: list<Expr>
    }

let NoVars e =
    { Vars = Map.empty; Expr = e }

let WithVars vars e =
    { Vars = vars; Expr = e }

let HasVars p =
    not (Map.isEmpty p.Vars)

module Parsed =

    let private substVars (finalVars: Vars) (p: Parsed) =
        let substs = ([], p.Vars) ||> Map.fold (fun substs k type' ->
            match Map.tryFind k finalVars with
            | Some type2 when type' <> type2 ->
                { name = k; innerType = type'; outerType = type2 } :: substs
            | _ -> substs
        )
        match substs with
        | [] -> p.Expr
        | l -> [WrapVars(l, Concat p.Expr)]

    let private mergeConsecutiveTexts (exprs: seq<Expr>) : seq<Expr> =
        let currentHtml = StringBuilder()
        let res = ResizeArray()
        let pushHtml () =
            let s = currentHtml.ToString()
            if s <> "" then
                res.Add(PlainHtml s)
                currentHtml.Clear() |> ignore
        let rec go = function
            | Concat es ->
                List.iter go es
            | PlainHtml t ->
                currentHtml.Append(t) |> ignore
            | e ->
                pushHtml()
                res.Add(e)
        Seq.iter go exprs
        pushHtml()
        res :> _

    let Concat (p: seq<Parsed>) : Parsed =
        let vars =
            p
            |> Seq.map (fun p -> p.Vars)
            |> Vars.MergeMany
        let exprs =
            p
            |> Seq.collect (substVars vars)
            |> mergeConsecutiveTexts
            |> List.ofSeq
        WithVars vars exprs

    let Map2 (f: list<Expr> -> list<Expr> -> list<Expr>) (p1: Parsed) (p2: Parsed) : Parsed =
        let vars = Vars.Merge p1.Vars p2.Vars
        let e1 = substVars vars p1
        let e2 = substVars vars p2
        WithVars vars (f e1 e2)

let ParseText (t: string) (varType: HoleType) : Parsed =
    let parse = HoleRE.Matches(t) |> Seq.cast<Match> |> Array.ofSeq
    if Array.isEmpty parse then NoVars [PlainHtml t] else
    let parts = ResizeArray()
    let mutable lastHoleEnd = 0
    let mutable vars = Map.empty
    for p in parse do
        if p.Index > lastHoleEnd then
            parts.Add(PlainHtml t[lastHoleEnd..p.Index - 1])
        let varName = p.Groups[1].Value
        if not (Map.containsKey varName vars) then
            vars <- Map.add varName varType vars
        parts.Add(VarContent varName)
        lastHoleEnd <- p.Index + p.Length
    if lastHoleEnd < t.Length then
        parts.Add(PlainHtml t[lastHoleEnd..t.Length - 1])
    WithVars vars (parts.ToArray() |> List.ofSeq)

let ParseAttribute (ownerNode: HtmlNode) (attr: HtmlAttribute) : Parsed =
    let name = attr.Name
    let parsed = ParseText attr.Value HoleType.String
    match name, parsed.Expr with
    | _, [VarContent varName] ->
        if name = "attr" then
            WithVars (Map [varName, Attribute]) parsed.Expr
        else
            WithVars (Map [varName, AttributeValue]) [Attr(name, VarContent varName)]
    | _ ->
        WithVars parsed.Vars [Attr(name, Concat parsed.Expr)]

let rec ParseNode (optimizeHtml: bool) (node: HtmlNode) : Parsed =
    match node.NodeType with
    | HtmlNodeType.Element ->
        let name = node.Name
        let attrs =
            node.Attributes
            |> Seq.map (ParseAttribute node)
            |> Parsed.Concat
        let children =
            node.ChildNodes
            |> Seq.map (ParseNode optimizeHtml)
            |> Parsed.Concat
        if optimizeHtml && not (HasVars attrs || HasVars children) then
            NoVars [PlainHtml node.OuterHtml]
        else
            (attrs, children)
            ||> Parsed.Map2 (fun attrs children ->
                [Elt (name, attrs, children)])
    | HtmlNodeType.Text ->
        ParseText (node :?> HtmlTextNode).InnerHtml HoleType.Html
    | _ ->
        NoVars []

let ParseOneTemplate (optimizeHtml: bool) (nodes: HtmlNodeCollection) : Parsed =
    nodes
    |> Seq.map (ParseNode optimizeHtml)
    |> Parsed.Concat

type ParsedTemplates =
    {
        Filename: option<string>
        Main: Parsed
        Nested: Map<string, Parsed>
    }

let ParseDoc (optimizeHtml: bool) (filename: option<string>) (doc: HtmlDocument) : ParsedTemplates =
    let nested =
        let templateNodes =
            match doc.DocumentNode.SelectNodes("//template") with
            | null -> [||]
            | nodes -> Array.ofSeq nodes
        // Remove all template nodes from the document before processing,
        // so nested templates don't appear in their parent's content.
        templateNodes
        |> Seq.iter (fun n -> n.Remove())
        templateNodes
        |> Seq.map (fun n ->
            match n.GetAttributeValue("id", null) with
            | null ->
                failwith "Nested template must have an id"
            | id ->
                let parsed = ParseOneTemplate optimizeHtml n.ChildNodes
                (id, parsed)
        )
        |> Map.ofSeq
    let main = ParseOneTemplate optimizeHtml doc.DocumentNode.ChildNodes
    { Filename = filename; Main = main; Nested = nested }

let GetDoc (fileOrContent: string) (rootFolder: string) : option<string> * HtmlDocument =
    let doc = HtmlDocument()
    doc.OptionOutputOriginalCase <- true
    doc.OptionDefaultUseOriginalName <- true
    if fileOrContent.Contains("<") then
        doc.LoadHtml(fileOrContent)
        None, doc
    else
        let rootFolder = Path.Canonicalize rootFolder
        let fullPath = System.IO.Path.Combine(rootFolder, fileOrContent) |> Path.Canonicalize
        doc.Load(fullPath)
        Some (Path.GetRelativePath rootFolder fullPath), doc

let ParseFileOrContent (fileOrContent: string) (rootFolder: string) (optimizeHtml: bool) : ParsedTemplates =
    GetDoc fileOrContent rootFolder
    ||> ParseDoc optimizeHtml
