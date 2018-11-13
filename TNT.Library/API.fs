module TNT.Library.API

open System.Text
open System.Runtime.CompilerServices
open FunToolbox.FileSystem
open TNT.Model
open TNT.Library
open TNT.Library.Output
open TNT.Library.MachineTranslation
open TNT.Library.APIHelper

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

    return Ok()
}

let status (verbose: bool) = 
    withSourcesAndGroup ^ fun sources group -> output {

    if verbose then
        do! printProperties 0 sources.Format

    match TranslationGroup.translations group with
    | [] -> 
        yield I ^ "No translations, use 'tnt add' to add one."
    | translations ->
        for translation in translations do
            yield I ^ Translation.status translation

    return Ok()
}

/// Add a new language.
let addLanguage (language: LanguageTag) = 
    withGroup ^ fun group -> output {

    do! warnIfUnsupported language

    let translations = 
        group
        |> TranslationGroup.addLanguage language
        |> Option.toList
        
    do! commitTranslations "added" translations
    return Ok()
}

/// Add a new assembly.
let addAssembly (assemblyPath: AssemblySource rpath) = 
    withSources ^ fun sources -> output {

    let assemblySource = AssemblySource(assemblyPath)

    if sources.Sources.Contains assemblySource 
    then
        yield I ^ sprintf "Assembly '%s' is already listed as a translation source." (string assemblyPath)
        return Ok()
    else

    yield I ^ sprintf "Adding '%s' as translation source, use 'tnt extract' to update the translation files." (string assemblyPath)

    let sourcesPath = Directory.current() |> Path.extend Sources.path
    Sources.save sourcesPath { 
        sources with 
            Sources = Set.add assemblySource sources.Sources 
    }
    return Ok()
}

let removeAssembly (assemblyPath: AssemblySource rpath) = 
    withSources ^ fun sources -> output {

    let matches = 
        sources.Sources
        |> Seq.choose ^ function
            | AssemblySource path ->
                let pathsToCompare = 
                    RPath.parts path
                    |> List.subs
                    |> List.map RPath.ofParts
                if pathsToCompare |> List.contains assemblyPath 
                then Some ^ AssemblySource path
                else None
        |> Seq.toList
    
    match matches with
    | [] ->
        yield E ^ "found no assembly"
        return Error()

    | [source] ->
        yield I ^ sprintf "removing source:"
        do! printProperties 1 [source.Format]

        let newSources = 
            { sources with
                Sources = sources.Sources |> Set.remove source }

        let sourcesPath = Directory.current() |> Path.extend Sources.path
        newSources |> Sources.save sourcesPath

        return Ok()

    | moreThanOne ->
        yield E ^ "found more than one source that matches the relative path of the assembly:"
        for path in moreThanOne do
            yield E ^ indent ^ string path
        return Error()
}

let extract() = withSourcesAndGroup ^ fun sources group -> output {

    let newStrings, errors = 
        sources 
        |> Sources.extractOriginalStrings (Directory.current()) 

    let updated = 
        TranslationGroup.translations group
        |> List.choose ^ Translation.update newStrings

    do! commitTranslations "updated" updated

    if errors <> [] then
        yield W ^ "Some .t() invocations failed to extract text:"
        do! printProperties 1 ^ StringExtractor.ExtractionErrors.format errors

    return Ok()
}

let gc() = withGroup ^ fun group -> output {

    let collected = 
        group 
        |> TranslationGroup.translations
        |> List.choose Translation.gc

    do! commitTranslations "garbage collected" collected
    return Ok()
}

let projectName() = 
    Directory.current() 
    |> Path.name 
    |> ProjectName

let private selectTranslations (languages: LanguageTag selector) (translations: Translation seq) : Translation seq =
    translations
    |> Seq.filter ^ fun translation -> 
        Selector.isSelected translation.Language languages
        || Selector.isSelected translation.Language.Primary languages

let export 
    (languages: LanguageTag selector)
    (exportDirectory: ARPath) 
    (profile: XLIFF.ExportProfile) =
    withSourcesAndGroup ^ fun sources group -> output {

    let project = projectName()
    let exports = 
        group
        |> TranslationGroup.translations
        |> selectTranslations languages
        |> Seq.map ^ fun translation ->
            let filename = XLIFF.defaultFilenameForLanguage project translation.Language 
            let path = exportDirectory |> ARPath.extend ^ RelativePath (string filename)
            let file = ImportExport.export project sources.Language translation
            path, XLIFF.generateV12 profile [file]

    let rooted = ARPath.at ^ Directory.current()

    let existingOnes = 
        exports
        |> Seq.map fst
        |> Seq.filter ^ fun path -> File.exists (rooted path)
        |> Seq.toList

    if existingOnes <> [] then
        yield E ^ sprintf "One or more exported files already exists, please remove them:"
        for existingFile in existingOnes do
            yield E ^ indent ^ string existingFile
        return Error()
    else

    for (file, content) in exports do
        yield I ^ sprintf "Exporting translation to '%s'" (string file)
        File.saveText Encoding.UTF8 (string content) (rooted file)

    return Ok()
}

let import (files: Path list) = withGroup ^ fun group -> output {

    let project = projectName()
    let files = 
        files
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
    return Ok()
}

let translate (languages: LanguageTag selector) = 
    withSourcesAndGroup ^ fun sources group -> output {

    let toTranslate = 
        group
        |> TranslationGroup.translations
        |> selectTranslations languages
        |> Seq.toList

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

    return Ok()
}

let sync() = output {
    do! syncAllContent()
    return Ok()
}

[<AutoOpen>]
module internal ShowHelper =

    let showOriginalStringsWithContext languages context recordFilter = 
        withGroup ^ fun group -> output {

        let filteredOriginalStrings = 
            group
            |> TranslationGroup.translations
            |> selectTranslations languages
            |> Seq.collect ^ fun t -> t.Records
            |> Seq.filter recordFilter
            |> Seq.map ^ fun r -> r.Original, r.Contexts
            |> OriginalStrings.create
            |> OriginalStrings.format
            |> List.toArray

        match filteredOriginalStrings with
        | [||] ->
            yield I ^ sprintf "No %s" context
            return Ok()
        | strings ->
            yield I ^ sprintf "%d %s:" strings.Length context
            do! printIndentedStrings 1 strings
            return Ok()
    }

    let showNew languages = 
        showOriginalStringsWithContext languages "new strings"
            ^ fun r -> 
                r.Translated 
                |> function TranslatedString.New -> true | _ -> false

    let showUnused languages = 
        showOriginalStringsWithContext languages "unused strings"
            ^ fun r ->
                r.Translated
                |> function TranslatedString.Unused _ -> true | _ -> false

    let showShared languages = 
        showOriginalStringsWithContext languages "shared strings"
            ^ fun r -> 
                match r.Contexts with
                | [] | [_] -> false
                | _ -> true

    let showWarnings languages = withGroup ^ fun group -> output {
        
        let translationsWithWarnings = 
            group
            |> TranslationGroup.translations
            |> selectTranslations languages
            |> Seq.map ^ fun translation ->
                translation.Language,
                translation.Records
                |> List.choose ^ fun record -> 
                    match Verification.verifyRecord record with
                    | [] -> None
                    | warnings -> Some (record, warnings)
            |> Seq.toList

        match translationsWithWarnings with
        | [] -> 
            yield I ^ "No Warnings"
        | translations ->
            for language, records in translations do
                yield I ^ sprintf "%s Warnings:" language.Formatted
                for record, warnings in records do
                    let warningProperties = 
                        warnings 
                        |> List.map ^ fun warning ->
                            Format.prop "warning" (string warning)
                    do! printProperties 1 (warningProperties)
                    do! printProperties 2 (TranslationRecord.format record)

        return Ok()
    }

let show (languages: LanguageTag selector) (details: string list) = output {

    for detail in details do
        match detail with
        | "languages" -> 
            yield I ^ "Supported languages:"
            for { Tag = tag; EnglishName = name } in SystemCultures.All do
                yield I ^ indent ^ sprintf "%s %s" tag.Formatted name.Formatted
        | "unused" ->
            do! showUnused languages |> Output.ignore
        | "shared" ->
            do! showShared languages |> Output.ignore
        | "new" ->
            do! showNew languages |> Output.ignore
        | "warnings" ->
            do! showWarnings languages |> Output.ignore

        | unsupported -> 
            yield E ^ sprintf "unsupported category: %s" unsupported
        
    return Ok()
}

[<assembly:InternalsVisibleTo("TNT.Tests")>]
do ()