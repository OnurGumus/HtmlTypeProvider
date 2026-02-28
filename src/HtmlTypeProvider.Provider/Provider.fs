namespace HtmlTypeProvider.Templating

open System
open System.Collections.Concurrent
open System.IO
open System.Reflection
open System.Threading
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open HtmlTypeProvider.TemplatingInternals

module internal StaticCache =
    let maxParsedTemplateCacheEntries = 256
    let parsedTemplateCache = ConcurrentDictionary<string, Parsing.ParsedTemplates>()
    let parsedTemplateCacheOrder = ConcurrentQueue<string>()
    let providedTypeCache = ConcurrentDictionary<string, ProvidedTypeDefinition>()
    let providedTypeKeyToParseKey = ConcurrentDictionary<string, string>()
    let parseKeyToPath = ConcurrentDictionary<string, string>()

[<TypeProvider>]
type Template (cfg: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(cfg,
        assemblyReplacementMap = ["HtmlTypeProvider.Provider", "HtmlTypeProvider"],
        addDefaultProbingLocation = true)

    let thisAssembly = Assembly.GetExecutingAssembly()
    let rootNamespace = "HtmlTypeProvider"
    let resolutionFolder = Path.Canonicalize cfg.ResolutionFolder
    let enableWatch =
        match Environment.GetEnvironmentVariable("HTMLTYPEPROVIDER_WATCH") with
        | "0" | "false" -> false
        | _ -> true

    // Polling-based file watching: deduped by physical file path
    let watchedPaths = ConcurrentDictionary<string, DateTime>()     // fullPath -> lastWriteTimeUtc
    let debounceTimer = ref Option<Timer>.None
    let debounceTimerLock = obj()
    let pollTimer = ref Option<Timer>.None
    let pollTimerLock = obj()

    let rec trimCache maxEntries (order: ConcurrentQueue<string>) (cache: ConcurrentDictionary<string, 'T>) (keyToPath: ConcurrentDictionary<string, string>) =
        if cache.Count > maxEntries then
            let mutable staleKey = Unchecked.defaultof<string>
            if order.TryDequeue(&staleKey) then
                cache.TryRemove staleKey |> ignore
                keyToPath.TryRemove staleKey |> ignore
                trimCache maxEntries order cache keyToPath

    let debounceInvalidate () =
        lock debounceTimerLock (fun _ ->
            debounceTimer.Value |> Option.iter (fun (t: Timer) -> t.Dispose())
            debounceTimer.Value <- Some (new Timer((fun _ ->
                lock debounceTimerLock (fun _ -> debounceTimer.Value <- None)
                this.Invalidate()), null, 300, Timeout.Infinite)))

    let rec checkFiles _ =
        for KeyValue(fullPath, lastWrite) in watchedPaths do
            let changed =
                try
                    File.GetLastWriteTimeUtc(fullPath) <> lastWrite
                with _ -> true
            if changed then
                watchedPaths.TryRemove fullPath |> ignore
                for KeyValue(key, path) in StaticCache.parseKeyToPath do
                    if path = fullPath then
                        StaticCache.parsedTemplateCache.TryRemove key |> ignore
                        StaticCache.parseKeyToPath.TryRemove key |> ignore
                        for KeyValue(typeKey, parseKey) in StaticCache.providedTypeKeyToParseKey do
                            if parseKey = key then
                                StaticCache.providedTypeCache.TryRemove typeKey |> ignore
                                StaticCache.providedTypeKeyToParseKey.TryRemove typeKey |> ignore
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
    and ensureTimer () =
        lock pollTimerLock (fun _ ->
            if pollTimer.Value.IsNone && not (watchedPaths.IsEmpty) then
                pollTimer.Value <- Some (new Timer(checkFiles, null, 2000, Timeout.Infinite)))
    and watchFile key fileName =
        let fullPath = Path.Combine(resolutionFolder, fileName) |> Path.Canonicalize
        try
            let lastWrite = File.GetLastWriteTimeUtc(fullPath)
            watchedPaths.[fullPath] <- lastWrite
            StaticCache.parseKeyToPath.[key] <- fullPath
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
                let parseKey = $"{resolutionFolder}|{pathOrHtml}|{optimizeHtml}"
                let content =
                    StaticCache.parsedTemplateCache.GetOrAdd(parseKey, fun key ->
                        let parsed = Parsing.ParseFileOrContent pathOrHtml resolutionFolder optimizeHtml
                        if enableWatch then
                            parsed.Filename |> Option.iter (fun fileName -> watchFile key fileName)
                        StaticCache.parsedTemplateCacheOrder.Enqueue key
                        trimCache StaticCache.maxParsedTemplateCacheEntries StaticCache.parsedTemplateCacheOrder StaticCache.parsedTemplateCache StaticCache.parseKeyToPath
                        parsed)
                let typeKey = $"{parseKey}|{typename}"
                StaticCache.providedTypeCache.GetOrAdd(typeKey, fun key ->
                    let asm = ProvidedAssembly()
                    let ty = ProvidedTypeDefinition(asm, rootNamespace, typename, Some typeof<TemplateNode>,
                                isErased = false,
                                hideObjectMethods = true)
                    CodeGen.Populate ty content
                    asm.AddTypes([ty])
                    StaticCache.providedTypeKeyToParseKey.[key] <- parseKey
                    ty)
            | x -> failwith $"Unexpected parameter values: {x}"
        )
        templateTy.AddXmlDoc("Provide content from a template HTML file.")
        this.AddNamespace(rootNamespace, [templateTy])
        with exn ->
            failwith $"{exn}"
