namespace HtmlTypeProvider

open System.Text

type Attr = delegate of StringBuilder -> unit

type Node = delegate of StringBuilder -> unit
