namespace HtmlTypeProvider.Templating

open System
open System.Collections.Concurrent
open System.IO
open System.Reflection
open System.Threading
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open HtmlTypeProvider.TemplatingInternals

[<TypeProvider>]
type Template (cfg: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(cfg,
        assemblyReplacementMap = ["HtmlTypeProvider.Provider", "HtmlTypeProvider"],
        addDefaultProbingLocation = true)

    let thisAssembly = Assembly.GetExecutingAssembly()
    let rootNamespace = "HtmlTypeProvider"
    let cache = ConcurrentDictionary<string, ProvidedTypeDefinition>()

    // Polling-based file watching: deduped by physical file path
    let watchedPaths = ConcurrentDictionary<string, DateTime>()   // fullPath -> lastWriteTimeUtc
    let keyToPath = ConcurrentDictionary<string, string>()        // cacheKey -> fullPath
    let debounceTimer = ref Option<Timer>.None
    let debounceTimerLock = obj()
    let pollTimer = ref Option<Timer>.None
    let pollTimerLock = obj()

    let debounceInvalidate () =
        lock debounceTimerLock (fun _ ->
            debounceTimer.Value |> Option.iter (fun (t: Timer) -> t.Dispose())
            debounceTimer.Value <- Some (new Timer((fun _ ->
                lock debounceTimerLock (fun _ -> debounceTimer.Value <- None)
                this.Invalidate()), null, 300, Timeout.Infinite)))

    let checkFiles _ =
        for KeyValue(fullPath, lastWrite) in watchedPaths do
            let changed =
                try
                    File.GetLastWriteTimeUtc(fullPath) <> lastWrite
                with _ -> true
            if changed then
                watchedPaths.TryRemove fullPath |> ignore
                for KeyValue(key, path) in keyToPath do
                    if path = fullPath then
                        cache.TryRemove key |> ignore
                        keyToPath.TryRemove key |> ignore
                debounceInvalidate()
        // Re-arm timer only if there are still files to watch; otherwise stop
        lock pollTimerLock (fun _ ->
            match pollTimer.Value with
            | Some t when not (watchedPaths.IsEmpty) ->
                t.Change(2000, Timeout.Infinite) |> ignore
            | Some t ->
                t.Dispose()
                pollTimer.Value <- None
            | None -> ())

    let ensureTimer () =
        lock pollTimerLock (fun _ ->
            if pollTimer.Value.IsNone && not (watchedPaths.IsEmpty) then
                pollTimer.Value <- Some (new Timer(checkFiles, null, 2000, Timeout.Infinite)))

    let watchFile key fileName =
        let fullPath = Path.Combine(cfg.ResolutionFolder, fileName) |> Path.Canonicalize
        try
            let lastWrite = File.GetLastWriteTimeUtc(fullPath)
            watchedPaths.[fullPath] <- lastWrite
            keyToPath.[key] <- fullPath
            ensureTimer()
        with _ -> ()

    let dispose () =
        lock pollTimerLock (fun _ ->
            pollTimer.Value |> Option.iter (fun t -> t.Dispose())
            pollTimer.Value <- None)
        lock debounceTimerLock (fun _ ->
            debounceTimer.Value |> Option.iter (fun (t: Timer) -> t.Dispose())
            debounceTimer.Value <- None)
        watchedPaths.Clear()
        keyToPath.Clear()
        cache.Clear()

    do this.Disposing.Add(fun _ -> dispose())

    do try
        let templateTy = ProvidedTypeDefinition(thisAssembly, rootNamespace, "Template", None, isErased = false)
        let pathOrHtmlParam = ProvidedStaticParameter("pathOrHtml", typeof<string>)
        pathOrHtmlParam.AddXmlDoc("The path to an HTML file, or an HTML string directly.")
        let optimizeHtmlParam = ProvidedStaticParameter("optimizePlainHtml", typeof<bool>, true)
        optimizeHtmlParam.AddXmlDoc("Optimize the rendering of HTML segments that don't contain any holes.")
        templateTy.DefineStaticParameters([pathOrHtmlParam; optimizeHtmlParam], fun typename pars ->
            match pars with
            | [| :? string as pathOrHtml; :? bool as optimizeHtml |] ->
                let cacheKey = $"{typename}|{pathOrHtml}|{optimizeHtml}"
                cache.GetOrAdd(cacheKey, fun key ->
                    let asm = ProvidedAssembly()
                    let ty = ProvidedTypeDefinition(asm, rootNamespace, typename, Some typeof<TemplateNode>,
                                isErased = false,
                                hideObjectMethods = true)
                    let content = Parsing.ParseFileOrContent pathOrHtml cfg.ResolutionFolder optimizeHtml
                    CodeGen.Populate ty content
                    asm.AddTypes([ty])
                    content.Filename |> Option.iter (watchFile key)
                    ty)
            | x -> failwith $"Unexpected parameter values: {x}"
        )
        templateTy.AddXmlDoc("Provide content from a template HTML file.")
        this.AddNamespace(rootNamespace, [templateTy])
        with exn ->
            failwith $"{exn}"
