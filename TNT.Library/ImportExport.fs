module TNT.Library.ImportExport

open TNT.Model
open TNT.Library.XLIFF

/// Convert a translations to a file.
let export (project: ProjectName) (sourceLanguage: LanguageTag) (translation: Translation)  : File =

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

    let toFile (translation: Translation) = {
        Name = string project
        SourceLanguage = sourceLanguage
        TargetLanguage = translation.Language
        TranslationUnits = translation.Records |> List.choose toUnit
    }

    toFile translation

type ImportWarning = 
    | ProjectMismatch of ProjectName * ProjectName
    | DuplicateImports of LanguageTag
    | TranslationNotFound of LanguageTag
    | OriginalStringNotFound of LanguageTag * TranslationUnit
    | UnusedTranslationChanged of LanguageTag * (TranslationRecord * TranslationRecord)
    | IgnoredNewWithTranslation of LanguageTag * (TranslationRecord * TranslationUnit)
    | IgnoredNewReset of LanguageTag * (TranslationRecord * TranslationUnit)
    override this.ToString() =
        match this with
        | ProjectMismatch(wrong, expected)
            -> sprintf "'%O': unexpected project name of <file>, expect '%O'." wrong expected
        | DuplicateImports language 
            -> sprintf "[%O] found two or more files with the same language" language
        | TranslationNotFound language 
            -> sprintf "[%O] translation missing" language
        | OriginalStringNotFound(language, tu) 
            -> sprintf "[%O] original string not found: '%s'" language tu.Source
        | UnusedTranslationChanged(language, (before, _)) 
            -> sprintf "[%O] unused translation changed: '%s'" language before.Original
        | IgnoredNewWithTranslation(language, (record, _)) 
            -> sprintf "[%O] ignored translation of a record marked new: '%s'" language record.Original
        | IgnoredNewReset(language, (record, _)) 
            -> sprintf "[%O] ignored translation to state new, even though it wasn't new anymore: '%s'" language record.Original

/// Import a number of translations and return the translations that changed.
let import (project: ProjectName) (translations: Translation list) (files: File list) 
    : Translation list * ImportWarning list =

    // find files that does not belong to us.

    let mismatchedNames = 
        files 
        |> Seq.map ^ fun file -> ProjectName file.Name
        |> Seq.filter ^ (<>) project
        |> Seq.toList

    if mismatchedNames <> [] 
    then
        [], 
        mismatchedNames 
        |> List.map ^ fun wrong -> ProjectMismatch(wrong, project)
    else

    // find duplicated languages in the file list.

    let duplicatedLanguages = 
        files
        |> Seq.groupBy ^ fun file -> file.TargetLanguage
        |> Seq.filter ^ fun (_, files) -> Seq.length files > 1
        |> Seq.map fst
        |> Seq.toList

    if duplicatedLanguages <> [] 
    then [], duplicatedLanguages |> List.map DuplicateImports
    else
    
    // group translations, expect no duplicates.

    let translations = 
        translations 
        |> Seq.groupBy ^ fun translation -> translation.Language
        |> Seq.map ^ Snd.map ^ Seq.exactlyOne
        |> Map.ofSeq

    /// Import one file, and return the translation that changed.
    let tryImportFile
        (file: File) 
        : Translation option * ImportWarning list =
        
        let language = file.TargetLanguage

        match Map.tryFind language translations with
        | None -> None, [TranslationNotFound language]
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
                    newRecord, Some (UnusedTranslationChanged(language, (record, newRecord)))
                | _ ->
                    { record with Translated = translatedString }, None
                    
            let record, warningOpt = 
                match unit.State with
                | New when unit.Target <> "" 
                    -> record, Some ^ IgnoredNewWithTranslation(language, (record, unit))
                | New when record.Translated = TranslatedString.New 
                    -> record, None
                | New 
                    -> record, Some ^ IgnoredNewReset(language, (record, unit))
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

        let records = translation.Records

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
            |> Seq.map ^ fun (_, tu) -> OriginalStringNotFound(language, tu)
            |> Seq.toList

        let translation = 
            if newRecords <> records 
            then Some ^ { translation with Records = newRecords }
            else None

        translation, warnings @ stringsNotFoundWarnings

    files 
    |> List.map tryImportFile
    |> List.unzip
    |> T2.map (List.choose id) List.concat

    
