[<AutoOpen>]
module internal HtmlTypeProvider.Templating.Utilities

open FSharp.Quotations

module TExpr =

    let Array<'T> (items: seq<Expr<'T>>) : Expr<'T[]> =
        Expr.NewArray(typeof<'T>, [ for i in items -> upcast i ])
        |> Expr.Cast

    let Coerce<'T> (e: Expr) : Expr<'T> =
        if e.Type = typeof<'T> then e else Expr.Coerce(e, typeof<'T>)
        |> Expr.Cast
