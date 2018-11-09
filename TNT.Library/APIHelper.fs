module internal TNT.Library.APIHelper

open FunToolbox.FileSystem
open TNT.Model
open TNT.Library
open TNT.Library.Output

module TranslationGroup = 

    let errorString = function
        | TranslationGroup.TranslationsWithTheSameLanguage l ->
            l |> Seq.map ^ fun (language, _) ->
                E ^ sprintf "multiple translations of the same language: '%s'" (string language)

let [<Literal>] DefaultIndent = "  "
let indent str = DefaultIndent + str

let tryLoadSources(): Sources option output = output {
    let sourcesPath = Directory.current() |> Path.extend Sources.path
    if File.exists sourcesPath 
    then return Some ^ Sources.load sourcesPath
    else return None
}

let loadSources() : Result<Sources, unit> output = output {
    match! tryLoadSources() with
    | Some sources -> return Ok sources
    | None ->
        yield E ^ sprintf "Can't load '%O', use 'tnt init' to create it." Sources.path
        return Error()
}

let loadSourcesAndGroup() : Result<Sources * TranslationGroup, unit> output = output {
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

let loadGroup() =
    loadSourcesAndGroup()
    |> Output.map ^ Result.map snd

let translationsDirectory() = 
    Directory.current()
    |> Path.extend TNT.Subdirectory

/// Sync translations to the content directory. This is not done by synchronizing
/// all the given languages by checking of the existence and file dates in the 
/// .tnt and .tnt-content directories.
/// We are doing this to keep the synchronization incremental, independent, and to
/// support it as a separate command.
let syncContent
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
let syncAllContent() = output {

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
let commitTranslations 
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

let warnIfUnsupported (language: LanguageTag) : unit output = output {
    if not ^ SystemCultures.isSupported language then
        yield W ^ 
            sprintf "%s is not supported by your .NET installation." language.Formatted
        yield I ^ 
            sprintf "For a list of supported languages, enter 'tnt show languages'."
}

let printProperties (initialIndentLevel: int) (properties: Property list) : unit output = output {
    let strings = 
        properties
        |> Properties.strings (Indent(initialIndentLevel, DefaultIndent))
    for string in strings do
        yield I ^ string
}

let printIndentedStrings (initialIndentLevel: int) (strings: IndentedString list) : unit output = output {
    let strings = 
        strings
        |> IndentedStrings.strings (Indent(initialIndentLevel, DefaultIndent))
    for string in strings do
        yield I ^ string
}