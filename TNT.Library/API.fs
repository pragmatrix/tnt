module TNT.Library.API

open System.Text
open System.Runtime.CompilerServices
open FunToolbox.FileSystem
open TNT.Model
open TNT.Library
open TNT.Library.Output
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
    let sourcesPath = Directory.current() |> Path.extend Sources.path
    if File.exists sourcesPath 
    then return Some ^ Sources.load sourcesPath
    else return None
}

let private loadSources() : Result<Sources, unit> output = output {
    match! tryLoadSources() with
    | Some sources -> return Ok sources
    | None ->
        yield E ^ sprintf "Can't load '%O', use 'tnt init' to create it." Sources.path
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

let private translationsDirectory() = 
    Directory.current()
    |> Path.extend TNT.Subdirectory

/// Sync translations to the content directory. This is not done by synchronizing
/// all the given languages by checking of the existence and file dates in the 
/// .tnt and .tnt-content directories.
/// We are doing this to keep the synchronization incremental, independent, and to
/// support it as a separate command.
let private syncContent
    (languages: LanguageTag list) 
    (cache: Translation list) = output {

    let currentDirectory = Directory.current()

    let atCurrentDirectory rpath = 
        rpath |> RPath.at currentDirectory

    let translationPath language = 
        TNT.Subdirectory
        |> RPath.extendF ^ Translation.filenameOfLanguage language

    let contentPath language = 
        TNT.ContentSubdirectory
        |> RPath.extendF ^ TranslationContent.filenameOfLanguage language
    
    let translations, contents = 
        let filterByExistence mkPath =
            languages 
            |> Seq.filter (
                mkPath
                >> atCurrentDirectory
                >> File.exists)
            |> Set.ofSeq

        filterByExistence translationPath,
        filterByExistence contentPath

    let toCreate, toUpdate, toDelete = 
        Set.difference translations contents,
        Set.intersect contents translations,
        Set.difference contents translations

    let resolveTranslation = 
        let map = 
            cache
            |> Seq.map ^ fun t -> translationPath t.Language, t
            |> Map.ofSeq
        fun (path: Translation rpath) ->
            match map.TryFind path with
            | Some t -> t
            | None -> Translation.load (atCurrentDirectory path)

    let toCreate = 
        toCreate
        |> Seq.map ^ fun tag -> translationPath tag, contentPath tag
        |> Seq.toList

    if toCreate <> [] 
        && toUpdate = Set.empty 
        && toDelete = Set.empty 
        && not ^ Directory.exists (atCurrentDirectory TNT.ContentSubdirectory) then
        yield I ^ sprintf "creating directory: %s" (string TNT.ContentSubdirectory)
        Path.ensureDirectoryExists (atCurrentDirectory TNT.ContentSubdirectory)

    for (tPath, cPath) in toCreate do
        yield I ^ sprintf "creating: %s" (string cPath)
        resolveTranslation tPath
        |> TranslationContent.fromTranslation
        |> TranslationContent.save (atCurrentDirectory cPath)

    let toUpdate =
        toUpdate
        |> Seq.map ^ fun tag -> translationPath tag, contentPath tag
        |> Seq.filter ^ fun (tp, cp) -> 
            File.lastWriteTimeUTC (atCurrentDirectory tp) 
            >= File.lastWriteTimeUTC (atCurrentDirectory cp)
        |> Seq.toList

    for (tPath, cPath) in toUpdate do
        yield I ^ sprintf "updating: %s" (string cPath)
        resolveTranslation tPath
        |> TranslationContent.fromTranslation
        |> TranslationContent.save (atCurrentDirectory cPath)

    let toDelete = 
        toDelete
        |> Seq.map ^ contentPath
        
    for cPath in toDelete do
        yield I ^ sprintf "deleting: %s" (string cPath)
        File.delete (atCurrentDirectory cPath)
}

/// Sync the complete languages / content directory. 
/// Use translations in the cache if they used to the new content files.
let private syncAllContent() = output {

    let currentDirectory = Directory.current()

    let contentLanguages = 
        TranslationContents.scan currentDirectory
        |> Seq.choose ^ TranslationContent.languageOf 
        |> Set.ofSeq

    let translationLanguages = 
        Translations.scan currentDirectory
        |> Seq.choose ^ Translation.languageOf
        |> Set.ofSeq

    let languagesInvolved = 
        Set.union contentLanguages translationLanguages
        |> Set.toList

    do! syncContent languagesInvolved []
}    

/// Commit the given translations and synchronize the related content files.
let private commitTranslations 
    (descriptionOfChange: string)
    (changedTranslations: Translation list) = output {
    match changedTranslations with
    | [] ->
        yield I ^ "No translations changed"
    | translations ->
        yield I ^ sprintf "Translations %s:" descriptionOfChange
        // save them all
        let translationsDirectory = translationsDirectory()
        for translation in translations do
            do
                let translationPath = 
                    translation
                    |> Translation.filename
                    |> Filename.at translationsDirectory
                translation |> Translation.save translationPath
            yield I ^ indent ^ Translation.status translation

        let languagesInvolved = changedTranslations |> List.map ^ fun t -> t.Language
        do! syncContent languagesInvolved changedTranslations
}

let private warnIfUnsupported (language: LanguageTag) : unit output = output {
    if not ^ SystemCultures.isSupported language then
        yield W ^ 
            sprintf "%s is not supported by your .NET installation." language.Formatted
        yield I ^ 
            sprintf "For a list of supported languages, enter 'tnt show languages'."
}

/// Initialize TNT.
let init (language: LanguageTag option) = output {
    let path = Directory.current() |> Path.extend Sources.path 
    match! tryLoadSources() with
    | None ->
        Path.ensureDirectoryOfPathExists path
        yield I ^ sprintf "Initializing '%s'" (string TNT.Subdirectory)
        Sources.save path {
            Language = defaultArg language Sources.DefaultLanguage
            Sources = Set.empty
        }

    | Some sources -> 
        match language with
        | Some l when l <> sources.Language ->
            do! warnIfUnsupported l
            yield I ^ sprintf "Changing source language from %s to %s" sources.Language.Formatted l.Formatted
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

    do! warnIfUnsupported language

    let translations = 
        group
        |> TranslationGroup.addLanguage language
        |> Option.toList
        
    do! commitTranslations "added" translations
    return Succeeded
}

/// Add a new assembly.
let addAssembly (assemblyPath: AssemblySource rpath) : ResultCode output = output {
    match! loadSources() with
    | Error() -> return Failed
    | Ok(sources) ->

    let assemblySource = AssemblySource(assemblyPath)

    if sources.Sources.Contains assemblySource 
    then
        yield I ^ sprintf "Assembly '%s' is already listed as a translation source." (string assemblyPath)
        return Succeeded
    else

    yield I ^ sprintf "Adding '%s' as translation source, use 'tnt fetch' to update the translation files." (string assemblyPath)

    let sourcesPath = Directory.current() |> Path.extend Sources.path
    Sources.save sourcesPath { 
        sources with 
            Sources = Set.add assemblySource sources.Sources 
    }
    return Succeeded
}

let fetch() = output {
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
                XLIFF.filenameForLanguage project translation.Language 
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
        |> Seq.map ^ fun fn -> importDirectory |> Path.extendF fn
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
            fun (translation : Translation) -> 
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

let show (categories: string list): ResultCode output = output {

    for category in categories do
        match category with
        | "languages" -> 
            yield I ^ "supported languages:"
            for tag, name in SystemCultures.All |> Map.toSeq do
                yield I ^ sprintf "%s %s" tag.Formatted name.Formatted
        | unsupported -> 
            yield E ^ sprintf "unsupported category: %s" unsupported
        
    return Succeeded
}

[<assembly:InternalsVisibleTo("TNT.Tests")>]
do ()