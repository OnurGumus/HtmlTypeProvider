module HtmlTypeProvider.Templating.ConvertExpr

open System
open FSharp.Quotations
open HtmlTypeProvider

let TypeOf (holeType: Parsing.HoleType) : Type =
    match holeType with
    | Parsing.String -> typeof<string>
    | Parsing.Html -> typeof<Node>
    | Parsing.Attribute -> typeof<Attr>
    | Parsing.AttributeValue -> typeof<obj>

let WrapExpr (innerType: Parsing.HoleType) (outerType: Parsing.HoleType) (expr: Expr) : option<Expr> =
    if innerType = outerType then None else
    match innerType, outerType with
    | Parsing.Html, Parsing.String ->
        <@@ Node.Text %%expr @@>
    | Parsing.AttributeValue, Parsing.String ->
        Expr.Coerce(expr, typeof<obj>)
    | a, b -> failwith $"Hole name used multiple times with incompatible types ({a}, {b})"
    |> Some

let WrapAndConvert (vars: Map<string, Expr>) (subst: list<Parsing.VarSubstitution>) convert expr =
    let vars, addLets = ((vars, id), subst) ||> List.fold (fun (vars, addLets) wrap ->
        let unwrapped = vars[wrap.name]
        let wrapped = WrapExpr wrap.innerType wrap.outerType unwrapped
        let var = Var(wrap.name, TypeOf wrap.innerType)
        let addLets e = Expr.Let(var, defaultArg wrapped unwrapped, addLets e) |> Expr.Cast
        let vars = Map.add wrap.name (Expr.Var var) vars
        (vars, addLets)
    )
    addLets (convert vars expr)

let rec ConvertAttrTextPart (vars: Map<string, Expr>) (text: Parsing.Expr) : Expr<string> =
    match text with
    | Parsing.Concat texts ->
        let texts = TExpr.Array<string>(Seq.map (ConvertAttrTextPart vars) texts)
        <@ String.Concat %texts @>
    | Parsing.PlainHtml text ->
        <@ text @>
    | Parsing.VarContent varName ->
        let e : Expr<obj> = Expr.Coerce(vars[varName], typeof<obj>) |> Expr.Cast
        <@ (%e).ToString() @>
    | Parsing.WrapVars (subst, text) ->
        WrapAndConvert vars subst ConvertAttrTextPart text
    | Parsing.Attr _ | Parsing.Elt _ ->
        failwith $"Invalid text: {text}"

let rec ConvertAttrValue (vars: Map<string, Expr>) (text: Parsing.Expr) : Expr<obj> =
    let box e = Expr.Coerce(e, typeof<obj>) |> Expr.Cast
    match text with
    | Parsing.Concat texts ->
        let texts = TExpr.Array<string>(Seq.map (ConvertAttrTextPart vars) texts)
        box <@ String.Concat %texts @>
    | Parsing.PlainHtml text ->
        box <@ text @>
    | Parsing.VarContent varName ->
        box vars[varName]
    | Parsing.WrapVars (subst, text) ->
        WrapAndConvert vars subst ConvertAttrValue text
    | Parsing.Attr _ | Parsing.Elt _ ->
        failwith $"Invalid attr value: {text}"

let rec ConvertAttr (vars: Map<string, Expr>) (attr: Parsing.Expr) : Expr<Attr> =
    match attr with
    | Parsing.Concat attrs ->
        let attrs = TExpr.Array<Attr> (Seq.map (ConvertAttr vars) attrs)
        <@ Attr.Attrs %attrs @>
    | Parsing.Attr (name, value) ->
        let value = ConvertAttrValue vars value
        <@ Attr.Make name %value @>
    | Parsing.VarContent varName ->
        vars[varName] |> Expr.Cast
    | Parsing.WrapVars (subst, attr) ->
        WrapAndConvert vars subst ConvertAttr attr
    | Parsing.PlainHtml _ | Parsing.Elt _ ->
        failwith $"Invalid attribute: {attr}"

let rec ConvertRawText (vars: Map<string, Expr>) (text: Parsing.Expr) : Expr<string> =
    match text with
    | Parsing.Concat texts ->
        let texts = TExpr.Array<string>(Seq.map (ConvertRawText vars) texts)
        <@ String.Concat %texts @>
    | Parsing.PlainHtml text ->
        <@ text @>
    | Parsing.VarContent varName ->
        vars[varName] |> Expr.Cast
    | Parsing.WrapVars (subst, text) ->
        WrapAndConvert vars subst ConvertRawText text
    | Parsing.Attr _ | Parsing.Elt _ ->
        failwith $"Unexpected expression in raw text: {text}"

let rec ConvertNode (vars: Map<string, Expr>) (node: Parsing.Expr) : Expr<Node> =
    match node with
    | Parsing.Concat exprs ->
        let exprs = TExpr.Array<Node> (Seq.map (ConvertNode vars) exprs)
        <@ Node.Fragment %exprs @>
    | Parsing.PlainHtml string ->
        <@ Node.RawHtml string @>
    | Parsing.Elt (name, attrs, children) ->
        let attrs = TExpr.Array<Attr> (Seq.map (ConvertAttr vars) attrs)
        let children = TExpr.Array<Node> (Seq.map (ConvertNode vars) children)
        <@ Node.Elt name %attrs %children @>
    | Parsing.VarContent varName ->
        vars[varName] |> Expr.Cast
    | Parsing.WrapVars (subst, node) ->
        WrapAndConvert vars subst ConvertNode node
    | Parsing.Attr _ ->
        failwith $"Invalid node: {node}"
