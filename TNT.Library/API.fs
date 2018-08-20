module TNT.Library.API

open System.Runtime.CompilerServices
open FunToolbox.FileSystem
open TNT.Model
open TNT.Library.Output

module TranslationGroup = 

    let errorString = function
        | TranslationGroup.AssemblyPathsWithTheSameFilename l ->
            l |> Seq.map ^ fun (fn, paths) ->
                E ^ sprintf "same filename, but different paths: '%s' : %A" (string fn) paths
        | TranslationGroup.TranslationsWithTheSameLanguage l ->
            l |> Seq.map ^ fun ((fn, language), _) ->
                E ^ sprintf "multiple translations of the same language: '%s' of '%s'" (string language)  (string fn)

let extract (assemblyPath: AssemblyPath) : OriginalString list output = output {
    let strings = StringExtractor.extract assemblyPath
    yield I ^ sprintf "extracted %d strings from '%s'" (strings.Length) (string assemblyPath)
    return strings
}

type ResultCode =
    | Failed
    | Succeeded

let createNewLanguage (language: LanguageIdentifier) (assemblyPath: AssemblyPath) : unit output = output {
    let assemblyFilename = assemblyPath |> AssemblyFilename.ofPath
    yield I ^ sprintf "found no language '%s' for '%s', adding one" (string language) (string assemblyFilename)
    let! strings = extract assemblyPath
    let id = TranslationId(assemblyPath, language)
    let filename = TranslationFilename.ofId id
    let translationPath = 
        Directory.current() |> Path.extend (string filename)
    Translation.createNew id strings
    |> Translation.save translationPath
    yield I ^ sprintf "new translation saved to '%s'" (string filename)
}

let add (language: LanguageIdentifier) (assembly: AssemblyPath option) : ResultCode output = output {
    let currentDirectory = Directory.current()
    let translations = Translations.loadAll (TranslationDirectory.ofPath currentDirectory)
    let group = TranslationGroup.fromTranslations translations
    match group with
    | Error(error) ->
        yield! TranslationGroup.errorString error
        return Failed
    | Ok(group) ->

    match assembly with
    | Some assemblyPath -> 
        let assemblyFilename = assemblyPath |> AssemblyFilename.ofPath
        let set = group |> TranslationGroup.tryGetSet assemblyFilename
        match set with
        | None -> 
            do! createNewLanguage language assemblyPath
            return Succeeded
        | Some set ->
            let setPath = TranslationSet.assemblyPath set
            if setPath <> assemblyPath then
                yield E ^ sprintf "assembly path '%s' in the translations files does not match '%s'" (string setPath) (string assemblyPath)
                return Succeeded
            else
            match set |> TranslationSet.translation language with
            | Some _ ->
                yield W ^ sprintf "language '%s' already exists for '%s', doing nothing" (string language) (string assemblyFilename)
                return Failed
            | None ->
                do! createNewLanguage language assemblyPath
                return Succeeded
    | None ->
        yield E "Adding a language to all available assemblies is unsupported yet"
        return Failed
}        

let update (assembly: AssemblyPath option) = output {
    yield E "not supported"
    return Failed
}

[<assembly:InternalsVisibleTo("TNT.Tests")>]
do ()