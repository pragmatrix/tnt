module TNT.Library.API

open System.Text
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

let extract (assembly: AssemblyInfo) : OriginalStrings output = output {
    let strings = StringExtractor.extract assembly
    let num = 
        strings
        |> OriginalStrings.strings
        |> List.length
    yield I ^ sprintf "extracted %d strings from '%s'" num (string assembly.Path)
    return strings
}

let private indent str = "  " + str

type ResultCode =
    | Failed
    | Succeeded

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

let private loadGroup() : Result<TranslationGroup, unit> output = output {
    let currentDirectory = Directory.current()
    match TranslationGroup.load currentDirectory with
    | Error(error) ->
        yield! TranslationGroup.errorString error
        return Error()
    | Ok(group) ->
        return Ok(group)
}

/// tbd: funtoolbox candidate. (also Seq & list)
module Array = 
    let (|IsEmpty|IsNotEmpty|) (array: 'e array) =
        match array with
        | [||] -> IsEmpty
        | _ -> IsNotEmpty array

let status() : ResultCode output = output {
    match! loadGroup() with
    | Error() ->
        return Failed
    | Ok(group) ->

    let translations = TranslationGroup.translations group
    match translations with
    | [] -> 
        yield I ^ "No translations found"
    | translations ->
        for translation in translations do
            yield I ^ Translation.status translation

    return Succeeded
}

let add (language: Language) (assemblyLanguage: Language option, assemblyPath: AssemblyPath option) : ResultCode output = output {
    match! loadGroup() with
    | Error() ->
        return Failed
    | Ok(group) ->

    match assemblyPath with
    | Some assemblyPath -> 
        let assemblyFilename = assemblyPath |> AssemblyFilename.ofPath
        let set = group |> TranslationGroup.set assemblyFilename
        match set with
        | None -> 
            let assembly = { 
                Language = defaultArg assemblyLanguage (Language "en-US")
                Path = assemblyPath 
            }
            do! createNewLanguage assembly language
            return Succeeded
        | Some set ->
            let setAssembly = TranslationSet.assembly set
            if setAssembly.Path <> assemblyPath then
                yield E ^ sprintf "assembly path '%s' in the translations files does not match '%s'" (string setAssembly.Path) (string assemblyPath)
                return Succeeded
            else
            match set |> TranslationSet.translation language with
            | Some _ ->
                yield W ^ sprintf "language '%s' already exists for '%s', doing nothing" (string language) (string assemblyFilename)
                return Failed
            | None ->
                do! createNewLanguage setAssembly language
                return Succeeded
    | None ->
        let translations =
            group
            |> TranslationGroup.addLanguage language
        match translations with
        | [] -> 
            yield I "No new translations were added."
        | translations -> 
            yield I "New translations:"
            for translation in translations do
                let translationPath = 
                    translation |> Translation.path (Directory.current())
                translation |> Translation.save translationPath 
                yield I ^ indent ^ Translation.status translation
        return Failed
}        

let update (assemblies: AssemblyFilename list) = output {
    match! loadGroup() with
    | Error() -> return Failed
    | Ok(group) ->

    let sets = 
        if assemblies = [] then 
            TranslationGroup.sets group 
        else
            assemblies 
            |> List.map ^ fun assembly -> 
                TranslationGroup.set assembly group 
                |> Option.defaultWith ^ fun () ->
                    failwithf "no translation for '%s'" (string assembly)
        
    for set in sets do
        let! strings = extract ^ TranslationSet.assembly set
        let updated = set |> TranslationSet.update strings
        for translation in updated do
            let translationPath = 
                translation |> Translation.path (Directory.current())
            translation |> Translation.save translationPath 
            yield I ^ indent ^ Translation.status translation
            
    return Failed
}

let export 
    (baseName: XLIFFBaseName)
    (outputDirectory: Path) 
    : ResultCode output = output {
    match! loadGroup() with
    | Error() ->
        return Failed
    | Ok(group) ->
    let allExports = 
        group
        |> TranslationGroup.translations
        |> List.groupBy ^ fun t -> t.Language
        |> Seq.map ^ fun (language, translations) -> 
            let path = 
                baseName
                |> XLIFFBaseName.filePathForLanguage language outputDirectory 
            let files = ImportExport.export translations
            path, XLIFF.generateV12 files

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

let import (files: Path list) : ResultCode output = output {
    let currentDirectory = Directory.current()
    match! loadGroup() with
    | Error() ->
        return Failed
    | Ok(group) ->
    let files = 
        files 
        |> Seq.map ^ File.loadText Encoding.UTF8
        |> Seq.map XLIFF.XLIFFV12
        |> Seq.collect XLIFF.parseV12
        |> Seq.toList

    let translations, warnings = 
        let translations = TranslationGroup.translations group
        files 
        |> ImportExport.import translations

    if warnings <> [] then
        yield I ^ "Import warnings:"
        for warning in warnings do
            yield W ^ indent ^ string warning

    match translations with
    | [] ->
        yield I ^ "No translations changed"
    | translations ->
        yield I ^ "Translations updated:"
        for translation in translations do
            let path = Translation.path currentDirectory translation
            translation |> Translation.save path 
            yield I ^ indent ^ Translation.status translation

    return Succeeded
}

[<assembly:InternalsVisibleTo("TNT.Tests")>]
do ()