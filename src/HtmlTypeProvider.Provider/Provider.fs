namespace HtmlTypeProvider.Templating

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
    let cache = ConcurrentDictionary<string, ProvidedTypeDefinition * FileSystemWatcher option>()
    let debounceTimer = ref Option<Timer>.None

    let watchFileChanges key fileName =
        let fullPath = Path.Combine(cfg.ResolutionFolder, fileName) |> Path.Canonicalize
        let fileWatcher = new FileSystemWatcher(
            Path = Path.GetDirectoryName(fullPath),
            Filter = Path.GetFileName(fullPath)
        )

        let lockObj = obj()
        let mutable disposed = false

        let changeHandler = fun _ ->
            lock lockObj (fun _ ->
                if disposed then () else
                cache.TryRemove key |> ignore
                fileWatcher.Dispose()
                disposed <- true
                // Debounce: coalesce rapid file change events into a single invalidation
                lock debounceTimer (fun _ ->
                    debounceTimer.Value |> Option.iter (fun (t: Timer) -> t.Dispose())
                    debounceTimer.Value <- Some (new Timer((fun _ ->
                        lock debounceTimer (fun _ -> debounceTimer.Value <- None)
                        this.Invalidate()), null, 300, Timeout.Infinite))))

        fileWatcher.Changed.Add(changeHandler)
        fileWatcher.Deleted.Add(changeHandler)
        fileWatcher.Renamed.Add(fun e -> changeHandler e)
        fileWatcher.EnableRaisingEvents <- true
        fileWatcher

    let disposeWatchers () =
        lock debounceTimer (fun _ ->
            debounceTimer.Value |> Option.iter (fun (t: Timer) -> t.Dispose())
            debounceTimer.Value <- None)
        for KeyValue(_, (_, watcher)) in cache do
            watcher |> Option.iter (fun w -> w.Dispose())
        cache.Clear()

    do this.Disposing.Add(fun _ -> disposeWatchers())

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
                let ty, _ =
                    cache.GetOrAdd(cacheKey, fun key ->
                        let asm = ProvidedAssembly()
                        let ty = ProvidedTypeDefinition(asm, rootNamespace, typename, Some typeof<TemplateNode>,
                                    isErased = false,
                                    hideObjectMethods = true)
                        let content = Parsing.ParseFileOrContent pathOrHtml cfg.ResolutionFolder optimizeHtml
                        CodeGen.Populate ty content
                        asm.AddTypes([ty])
                        let fileWatcher = content.Filename |> Option.map (watchFileChanges key)
                        ty, fileWatcher)
                ty
            | x -> failwith $"Unexpected parameter values: {x}"
        )
        templateTy.AddXmlDoc("Provide content from a template HTML file.")
        this.AddNamespace(rootNamespace, [templateTy])
        with exn ->
            failwith $"{exn}"
