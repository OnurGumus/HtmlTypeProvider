namespace HtmlTypeProvider

open System.Text

/// <summary>An HTML attribute that appends itself to a StringBuilder.</summary>
type Attr = delegate of StringBuilder -> unit

/// <summary>An HTML fragment that appends itself to a StringBuilder.</summary>
type Node = delegate of StringBuilder -> unit
