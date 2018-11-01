module TNT.Library.API

open System.Text
open System.Runtime.CompilerServices
open FunToolbox.FileSystem
open TNT.Model
open TNT.Library.Output
open TNT.Library.FileSystem
open TNT.Library.MachineTranslation

module TranslationGroup = 

    let errorString = function
        | TranslationGroup.TranslationsWithTheSameLanguage l ->
            l |> Seq.map ^ fun (language, _) ->
                E ^ sprintf "multiple translations of the same language: '%s'" (string language)

let [<Literal>] private DefaultIndent = "  "
let private indent str = DefaultIndent + str

type ResultCode =
    | Failed
    | Succeeded

let private tryLoadSources(): Sources option output = output {
    let currentDirectory = Directory.current()
    let sourcesPath = Sources.path currentDirectory
    if File.exists sourcesPath 
    then return Some ^ Sources.load sourcesPath
    else return None
}

let private loadSources() : Result<Sources, unit> output = output {
    match! tryLoadSources() with
    | Some sources -> return Ok sources
    | None ->
        yield E ^ sprintf "Can't load '%s/%s', use 'tnt init' to create it." TNT.Subdirectory Sources.SourcesFilename
        return Error()
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
let init (language: LanguageTag option) = output {
    let path = Sources.path (Directory.current())
    match! tryLoadSources() with
    | None ->
        Path.ensureDirectoryOfPathExists path
        yield I ^ sprintf "Initializing '%s'" TNT.Subdirectory
        Sources.save path {
            Language = defaultArg language Sources.DefaultLanguage
            Sources = Set.empty
        }

    | Some sources -> 
        match language with
        | Some l when l <> sources.Language ->
            yield I ^ sprintf "Changing source language from [%O] to [%O]" sources.Language l
            Sources.save path { sources with Language = l }
        | _ -> ()

    return Succeeded
}

let status (verbose: bool) : ResultCode output = output {
    match! loadSourcesAndGroup() with
    | Error() -> return Failed
    | Ok(sources, group) ->

    if verbose then
        let strings = 
            sources.Format
            |> Properties.strings DefaultIndent
        for string in strings do
            yield I ^ string

    match TranslationGroup.translations group with
    | [] -> 
        yield I ^ "No translations, use 'tnt add' to add one."
    | translations ->
        for translation in translations do
            yield I ^ Translation.status translation

    return Succeeded
}

/// Add a new language.
let addLanguage (language: LanguageTag) = output {
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

let private projectName() = 
    Directory.current() 
    |> Path.name 
    |> ProjectName

let export 
    (exportDirectory: ARPath) 
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
            let path = exportDirectory |> ARPath.extend ^ RelativePath (string filename)
            let file = ImportExport.export project sources.Language translation
            path, XLIFF.generateV12 [file]

    let rooted = ARPath.rooted ^ Directory.current()

    let existingOnes = 
        allExports
        |> Seq.map fst
        |> Seq.filter ^ fun path -> File.exists (rooted path)
        |> Seq.toList

    if existingOnes <> [] then
        yield E ^ sprintf "One or more exported files already exists, please remove them:"
        for existingFile in existingOnes do
            yield E ^ indent ^ string existingFile
        return Failed
    else

    for (file, content) in allExports do
        yield I ^ sprintf "Exporting translation to '%s'" (string file)
        File.saveText Encoding.UTF8 (string content) (rooted file)

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

let translate (languages: LanguageTag list) : ResultCode output = output {
    match! loadSourcesAndGroup() with
    | Error() -> return Failed
    | Ok(sources, group)  ->
    let toTranslate = 

        let filter = 
            if languages = [] then fun _ -> true
            else 
            fun translation -> 
                List.contains translation.Language languages
                || List.contains translation.Language.Primary languages

        group
        |> TranslationGroup.translations
        |> List.filter filter

    // note: the API may fail at any time, but if it does, continuing does not make
    // sense, but the translations done before should also not get lost, so we
    // translate and commit the results one by one.
    for translation in toTranslate do
        let result = 
            Translate.newStrings Google.Translator sources.Language translation
        yield I ^ (string result)
        match result with
        | Translated(_, translation) ->
            do! commitTranslations "translated" [translation]
        | _ -> ()

    return Succeeded
}

[<assembly:InternalsVisibleTo("TNT.Tests")>]
do ()