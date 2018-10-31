﻿module TNT.Library.API

open System.Text
open System.Runtime.CompilerServices
open FunToolbox.FileSystem
open TNT.Model
open TNT.Library.FileSystem
open TNT.Library.Output

module TranslationGroup = 

    let errorString = function
        | TranslationGroup.TranslationsWithTheSameLanguage l ->
            l |> Seq.map ^ fun (language, _) ->
                E ^ sprintf "multiple translations of the same language: '%s'" (string language)

(*
let extract (path: AssemblyPath) : OriginalStrings output = output {
    let strings = StringExtractor.extract path
    let num = 
        strings
        |> OriginalStrings.strings
        |> List.length
    yield I ^ sprintf "extracted %d strings from '%s'" num (string path)
    return strings
}

*)

let private indent str = "  " + str

type ResultCode =
    | Failed
    | Succeeded

(*
let createNewLanguage (assembly: AssemblyInfo) (language: Language) : unit output = output {
    let assemblyFilename = assembly.Path |> AssemblyFilename.ofPath
    yield I ^ sprintf "found no language '%s' for '%s', adding one" (string language) (string assemblyFilename)
    let! strings = extract assembly
    let translation = Translation.createNew language strings
    let translationPath = 
        translation |> Translation.path (Directory.current())
    yield I ^ "New translation:"
    translation |> Translation.save translationPath
    yield I ^ indent ^ Translation.status translation
}
*)

let private loadSources() : Result<Sources, unit> output = output {
    let currentDirectory = Directory.current()
    let sourcesPath = Sources.path currentDirectory
    if not ^ File.exists sourcesPath 
    then 
        yield E ^ sprintf "Can't find '%s/%s', use 'tnt init' to create it." TranslationSubdirectory Sources.SourcesFilename
        return Error()
    else
        return Ok ^ Sources.load sourcesPath
}

let private loadSourcesAndGroup() : Result<Sources * TranslationGroup, unit> output = output {
    match! loadSources() with
    | Error() -> return Error()
    | Ok sources ->
    let currentDirectory = Directory.current()
    match TranslationGroup.load currentDirectory with
    | Error(error) ->
        yield! TranslationGroup.errorString error
        return Error()
    | Ok(group) ->
        return Ok(sources, group)
}

let private loadGroup() =
    loadSourcesAndGroup()
    |> Output.map ^ Result.map snd

(*
/// tbd: funtoolbox candidate. (also Seq & list)
module Array = 
    let (|IsEmpty|IsNotEmpty|) (array: 'e array) =
        match array with
        | [||] -> IsEmpty
        | _ -> IsNotEmpty array

*)

let status() : ResultCode output = output {
    match! loadGroup() with
    | Error() ->
        return Failed
    | Ok(group) ->

    let translations = TranslationGroup.translations group
    match translations with
    | [] -> 
        yield I ^ "No translations, use 'tnt add' to add one."
    | translations ->
        for translation in translations do
            yield I ^ Translation.status translation

    return Succeeded
}

/// Update the given translations and update the combined file.
let private commitTranslations 
    (descriptionOfChange: string)
    (changedTranslations: Translation list) = output {
    match changedTranslations with
    | [] ->
        yield I ^ "No translations changed"
    | translations ->
        yield I ^ sprintf "Translations %s:" descriptionOfChange
        // save them all
        let currentDirectory = Directory.current()
        for translation in translations do
            let translationPath = 
                translation |> Translation.path currentDirectory
            translation |> Translation.save translationPath 
            yield I ^ indent ^ Translation.status translation
}

/// Initialize TNT.
let init (language: Language option) = output {
    match! loadSources() with
    | Ok _ -> return Succeeded
    | Error() ->
    let path = Sources.path (Directory.current())
    Path.ensureDirectoryOfPathExists path
    Sources.save path {
        Language = defaultArg language Sources.DefaultLanguage
        Sources = Set.empty
    }
    return Succeeded
}

/// Add a new language.
let addLanguage (language: Language) = output {
    match! loadGroup() with
    | Error() -> return Failed
    | Ok(group) ->

    let translations = 
        group
        |> TranslationGroup.addLanguage language
        |> Option.toList
        
    do! commitTranslations "added" translations
    return Succeeded
}

/// Add a new assembly.
let addAssembly (assemblyPath: AssemblyPath) : ResultCode output = output {
    match! loadSources() with
    | Error() -> return Failed
    | Ok(sources) ->

    let assemblySource = AssemblySource(assemblyPath)

    if sources.Sources.Contains assemblySource 
    then
        yield I ^ sprintf "Assembly '%s' is already listed as a translation source." (string assemblyPath)
        return Succeeded
    else

    yield I ^ sprintf "Adding '%s' as translation source, use 'tnt update' to update the translation files." (string assemblyPath)

    let sourcesPath = Directory.current() |> Sources.path
    Sources.save sourcesPath { 
        sources with 
            Sources = Set.add assemblySource sources.Sources 
    }
    return Succeeded
}

(*
let private setsOfAssemblies (assemblies: AssemblyFilename list) (group: TranslationGroup) =
    if assemblies = [] then 
        TranslationGroup.sets group 
    else
        assemblies 
        |> List.map ^ fun assembly -> 
            TranslationGroup.set assembly group 
            |> Option.defaultWith ^ fun () ->
                failwithf "no translation for '%s'" (string assembly)

*)

let update() = output {
    match! loadSourcesAndGroup() with
    | Error() -> return Failed
    | Ok(sources, group) ->

    let newStrings = 
        sources 
        |> Sources.extractOriginalStrings (Directory.current()) 

    let updated = 
        TranslationGroup.translations group
        |> List.choose ^ Translation.update newStrings

    do! commitTranslations "updated" updated
    return Succeeded
}

let gc() = output {
    match! loadGroup() with
    | Error() -> return Failed
    | Ok(group) ->

    let collected = 
        group 
        |> TranslationGroup.translations
        |> List.choose Translation.gc

    do! commitTranslations "garbage collected" collected
    return Succeeded
}

let private projectName() = Directory.current() |> Path.name |> ProjectName

let export 
    (exportDirectory: Path) 
    : ResultCode output = output {
    match! loadSourcesAndGroup() with
    | Error() ->
        return Failed
    | Ok(sources, group) ->
    let project = projectName()
    let allExports = 
        group
        |> TranslationGroup.translations
        |> Seq.map ^ fun translation ->
            let filename = 
                XLIFFFilename.filenameForLanguage project translation.Language 
            let path = exportDirectory |> Path.extend ^ string filename
            let file = ImportExport.export project sources.Language translation
            path, XLIFF.generateV12 [file]

    let existingOnes = 
        allExports
        |> Seq.map fst
        |> Seq.filter File.exists
        |> Seq.toList

    if existingOnes <> [] then
        yield E ^ sprintf "One or more exported files already exists, please remove them:"
        for existingFile in existingOnes do
            yield E ^ indent ^ string existingFile
        return Failed
    else

    for (file, content) in allExports do
        yield I ^ sprintf "Exporting translation to '%s'" (string file)
        File.saveText Encoding.UTF8 (string content) file

    return Succeeded
}

let import (importDirectory: Path) : ResultCode output = output {
    match! loadGroup() with
    | Error() ->
        return Failed
    | Ok(group) ->
    let project = projectName()
    let files = 
        XLIFFFilenames.inDirectory importDirectory project
        |> Seq.map ^ fun fn -> importDirectory |> Path.extend (string fn)
        |> Seq.map ^ File.loadText Encoding.UTF8
        |> Seq.map XLIFF.XLIFFV12
        |> Seq.collect XLIFF.parseV12
        |> Seq.toList

    let translations, warnings = 
        let translations = TranslationGroup.translations group
        files 
        |> ImportExport.import project translations

    if warnings <> [] then
        yield I ^ "Import warnings:"
        for warning in warnings do
            yield W ^ indent ^ string warning

    do! commitTranslations "changed by import" translations

    return Succeeded
}

[<assembly:InternalsVisibleTo("TNT.Tests")>]
do ()