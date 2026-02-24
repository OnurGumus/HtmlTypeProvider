module HtmlTypeProvider.TemplatingInternals

type TemplateNode() =
    member val Holes : obj[] = null with get, set

#if !IS_DESIGNTIME
[<assembly: FSharp.Core.CompilerServices.TypeProviderAssembly "HtmlTypeProvider.Provider">]
do ()
#endif
