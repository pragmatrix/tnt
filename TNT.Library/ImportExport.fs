module TNT.Library.ImportExport

open TNT.Model
open TNT.Library.XLIFF

/// Convert a number of translations to a list of files.
let export (translations: Translation list) : File list =

    let toUnit (record: TranslationRecord) =

        // note that not all records can be exported into XIFF files.
        record.Translated
        |> function
        | TranslatedString.New -> Some (New, "")
        | TranslatedString.NeedsReview str -> Some (NeedsReview, str)
        | TranslatedString.Final str -> Some (Final, str)
        | TranslatedString.Unused _ -> None
        |> Option.map ^ fun (state, str) ->
        {
            Source = record.Original
            Target = str
            State = state
        }

    let toFile (Translation(TranslationId(path, language), records)) = {
        Name = AssemblyFilename.ofPath path
        TargetLanguage = language
        TranslationUnits = records |> List.choose toUnit
    }

    translations
    |> List.map toFile

/// Import a number of files into a group, and return the new translations.

[<Struct>]
type ImportKey = 
    | ImportKey of AssemblyFilename * LanguageIdentifier
    override this.ToString() = 
        this |> function ImportKey(name, language) -> sprintf "[%O:%O]" name language

type ImportWarning = 
    | DuplicateImports of ImportKey
    | TranslationNotFound of ImportKey
    | OriginalStringNotFound of ImportKey * TranslationUnit
    | UnusedTranslationChanged of ImportKey * (TranslationRecord * TranslationRecord)
    | IgnoredNewWithTranslation of ImportKey * (TranslationRecord * TranslationUnit)
    | IgnoredNewReset of ImportKey * (TranslationRecord * TranslationUnit)
    override this.ToString() =
        match this with
        | DuplicateImports key 
            -> sprintf "%O: found two or more files with the same key" key
        | TranslationNotFound key 
            -> sprintf "%O: translation missing" key
        | OriginalStringNotFound(key, tu) 
            -> sprintf "%O: original string not found: '%s'" key tu.Source
        | UnusedTranslationChanged(key, (before, _)) 
            -> sprintf "%O: unused translation changed: '%s'" key before.Original
        | IgnoredNewWithTranslation(key, (record, tu)) 
            -> sprintf "%O: ignored translation of a record marked new :'%s'" key record.Original
        | IgnoredNewReset(key, (record, tu)) 
            -> sprintf "%O: ignored translation to state new, even though it wasn't new anymore: '%s'" key record.Original

let import (translations: Translation list) (files: File list) 
    : Translation list * ImportWarning list =

    // find duplicates in the file list.

    let key name lang = ImportKey(name, lang)
    let keyOfTranslation (translation: Translation) : ImportKey =
        key (Translation.assemblyFilename translation) (Translation.language translation)

    let keyOfFile file = 
        key file.Name file.TargetLanguage 

    let duplicates = 
        files
        |> Seq.groupBy ^ keyOfFile
        |> Seq.filter ^ fun (_, files) -> Seq.length files > 1
        |> Seq.map fst
        |> Seq.toList

    match duplicates with
    | _::_ -> [], duplicates |> List.map DuplicateImports
    | [] ->
    
    // group translations, expect no duplicates.

    let translations = 
        translations 
        |> Seq.groupBy keyOfTranslation
        |> Seq.map ^ Snd.map ^ Seq.exactlyOne
        |> Map.ofSeq

    /// Import one file, and return the translation that changed.
    let tryImportFile
        (file: File) 
        : Translation option * ImportWarning list =
        let key = keyOfFile file
        match Map.tryFind key translations with
        | None -> None, [TranslationNotFound key]
        | Some translation ->

        let updateRecord 
            (pending: Map<string, TranslationUnit>) (record: TranslationRecord) 
            : (TranslationRecord * ImportWarning option) * Map<string, TranslationUnit> =

            let original = record.Original
            
            match pending.TryFind original with
            | None -> (record, None), pending
            | Some unit ->

            let update translatedString = 
                match record.Translated with
                | TranslatedString.Unused _ ->
                    let newRecord = 
                        { record with 
                            Translated = TranslatedString.Unused (string translatedString) }
                    newRecord, Some (UnusedTranslationChanged(key, (record, newRecord)))
                | _ ->
                    { record with Translated = translatedString }, None
                    
            let record, warningOpt = 
                match unit.State with
                | New when unit.Target <> "" 
                    -> record, Some ^ IgnoredNewWithTranslation(key, (record, unit))
                | New when record.Translated = TranslatedString.New 
                    -> record, None
                | New 
                    -> record, Some ^ IgnoredNewReset(key, (record, unit))
                | NeedsReview 
                    -> update ^ TranslatedString.NeedsReview unit.Target
                | Translated | Final 
                    -> update ^ TranslatedString.Final unit.Target

            (record, warningOpt), Map.remove original pending

        let updates = 
            file.TranslationUnits
            |> Seq.groupBy ^ fun tu -> tu.Source
            |> Seq.map ^ Snd.map ^ Seq.exactlyOne
            |> Map.ofSeq

        let records = Translation.records translation

        let newRecordsAndWarnings, unprocessed = 
            (updates, records)
            ||> List.mapFold updateRecord

        let newRecords, warnings = 
            newRecordsAndWarnings 
            |> List.unzip
            |> Snd.map ^ List.choose id

        let stringsNotFoundWarnings = 
            unprocessed 
            |> Map.toSeq 
            |> Seq.map ^ fun (_, tu) -> OriginalStringNotFound(key, tu)
            |> Seq.toList

        let translation = 
            if newRecords <> records then
                Some ^ Translation(Translation.id translation, newRecords)
            else
                None

        translation, warnings @ stringsNotFoundWarnings

    files 
    |> List.map tryImportFile
    |> List.unzip
    |> T2.map (List.choose id) List.concat

    
