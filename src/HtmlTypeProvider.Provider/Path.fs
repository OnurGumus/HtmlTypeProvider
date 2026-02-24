module HtmlTypeProvider.Templating.Path

open System.IO

let Canonicalize (path: string) =
    FileInfo(path).FullName

let GetRelativePath (baseDir: string) (fullPath: string) =
    let rec go (thisDir: string) =
        if thisDir = baseDir then
            fullPath[thisDir.Length + 1..]
        elif thisDir.Length <= baseDir.Length then
            invalidArg "fullPath" $"'{fullPath}' is not a subdirectory of '{baseDir}'"
        else
            go (Path.GetDirectoryName thisDir)
    go fullPath
