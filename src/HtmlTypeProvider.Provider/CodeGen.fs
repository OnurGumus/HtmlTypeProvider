module HtmlTypeProvider.Templating.CodeGen

open System
open System.Reflection
open HtmlTypeProvider.Templating.Parsing
open FSharp.Quotations
open ProviderImplementation.ProvidedTypes
open HtmlTypeProvider
open HtmlTypeProvider.TemplatingInternals
open HtmlTypeProvider.Templating.ConvertExpr

let getThis (args: list<Expr>) : Expr<TemplateNode> =
    TExpr.Coerce<TemplateNode>(args[0])

let MakeCtor (holes: Parsing.Vars) =
    ProvidedConstructor([], fun args ->
        let holes = TExpr.Array<obj> [
            for KeyValue(_, type') in holes ->
                match type' with
                | HoleType.String -> <@ box "" @>
                | HoleType.Html -> <@ box (Node.Empty()) @>
                | HoleType.Attribute -> <@ box (Attr.Empty()) @>
                | HoleType.AttributeValue -> <@ box "" @>
        ]
        <@@ (%getThis args).Holes <- %holes @@>)

let HoleMethodBodies (holeType: HoleType) : (ProvidedParameter list * (Expr list -> Expr)) list =
    let (=>) name ty = ProvidedParameter(name, ty)
    match holeType with
    | HoleType.String ->
        [
            ["value" => typeof<string>], fun args ->
                <@@ box (%%args[1]: string) @@>
        ]
    | HoleType.Html ->
        [
            ["value" => typeof<string>], fun args ->
                <@@ box (Node.Text (%%args[1]: string)) @@>
            ["value" => typeof<Node>], fun args ->
                <@@ box (%%args[1]: Node) @@>
        ]
    | HoleType.Attribute ->
        [
            ["value" => typeof<Attr>], fun args ->
                <@@ box (%%args[1]: Attr) @@>
            ["value" => typeof<list<Attr>>], fun args ->
                <@@ box (Attr.Attrs(%%args[1]: list<Attr>)) @@>
        ]
    | HoleType.AttributeValue ->
        [
            ["value" => typeof<obj>], fun args ->
                <@@ %%args[1] @@>
        ]

let MakeHoleMethods (holeName: string) (holeType: HoleType) (index: int) (containerTy: ProvidedTypeDefinition) =
    [
        for args, value in HoleMethodBodies holeType do
            yield ProvidedMethod(holeName, args, containerTy, fun args ->
                let this = getThis args
                <@@ (%this).Holes[index] <- %%(value args)
                    %this @@>) :> MemberInfo
    ]

let MakeFinalMethod (content: Parsed) =
    ProvidedMethod("Elt", [], typeof<Node>, fun args ->
        let this = getThis args
        let vars = content.Vars |> Map.map (fun k v -> Var(k, TypeOf v))
        let varExprs = vars |> Map.map (fun _ v -> Expr.Var v)
        ((0, ConvertNode varExprs (Concat content.Expr) :> Expr), vars)
        ||> Seq.fold (fun (i, e) (KeyValue(_, var)) ->
            let value = <@@ (%this).Holes[i] @@>
            let value = Expr.Coerce(value, var.Type)
            i + 1, Expr.Let(var, value, e)
        )
        |> snd
    )

let MakeRenderMethod (content: Parsed) =
    ProvidedMethod("Render", [], typeof<string>, fun args ->
        let this = getThis args
        let vars = content.Vars |> Map.map (fun k v -> Var(k, TypeOf v))
        let varExprs = vars |> Map.map (fun _ v -> Expr.Var v)
        let nodeExpr = ConvertNode varExprs (Concat content.Expr)
        let bodyExpr =
            ((0, (<@@ Node.Render %nodeExpr @@> : Expr)), vars)
            ||> Seq.fold (fun (i, e) (KeyValue(_, var)) ->
                let value = <@@ (%this).Holes[i] @@>
                let value = Expr.Coerce(value, var.Type)
                i + 1, Expr.Let(var, value, e)
            )
            |> snd
        bodyExpr
    )

let PopulateOne (ty: ProvidedTypeDefinition) (content: Parsing.Parsed) =
    ty.AddMembers [
        yield MakeCtor content.Vars :> MemberInfo
        yield! content.Vars |> Seq.mapi (fun i (KeyValue(name, type')) ->
            MakeHoleMethods name type' i ty
        ) |> Seq.concat
        yield MakeFinalMethod content :> MemberInfo
        yield MakeRenderMethod content :> MemberInfo
    ]

let Populate (mainTy: ProvidedTypeDefinition) (content: ParsedTemplates) =
    PopulateOne mainTy content.Main
    for KeyValue(name, content) in content.Nested do
        let ty = ProvidedTypeDefinition(name, Some typeof<TemplateNode>,
                    isErased = false,
                    hideObjectMethods = true)
        mainTy.AddMember ty
        PopulateOne ty content
