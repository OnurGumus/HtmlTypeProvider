module HtmlTypeProvider.TemplatingInternals

type TemplateNode() =
    member val Holes : obj[] = null with get, set
    member val RuntimeTemplate : string = null with get, set
    member val HoleNames : string[] = null with get, set

#if !IS_DESIGNTIME
[<assembly: FSharp.Core.CompilerServices.TypeProviderAssembly "HtmlTypeProvider.Provider">]
do ()
#endif
