/// Support functions.
[<AutoOpen>]
module TNT.Library.Translation

open Newtonsoft.Json
open TNT.Model

module Seq = 
    let setify seq = seq |> Seq.sort |> Seq.distinct

module TranslationRecord = 

    let createNew original = { Original = original; Translated = TranslatedString.New }

    let unuse (record: TranslationRecord) : TranslationRecord option = 
        match record.Translated with
        | TranslatedString.New
            -> None
        | TranslatedString.NeedsReview str 
        | TranslatedString.Final str
        | TranslatedString.Unused str
            -> Some ^ { record with Translated = TranslatedString.Unused str }

    let reuse (record: TranslationRecord) : TranslationRecord = 
        match record.Translated with
        | TranslatedString.New _
        | TranslatedString.NeedsReview _
        | TranslatedString.Final _
            -> record
        | TranslatedString.Unused str
            -> { record with Translated = TranslatedString.NeedsReview str }

module TranslationCounters =

    let zero = { New = 0; NeedsReview = 0; Final = 0; Unused = 0 }
    
    let combine (l: TranslationCounters) (r: TranslationCounters) = {
        New = l.New + r.New
        NeedsReview = l.NeedsReview + r.NeedsReview
        Final = l.Final + r.Final
        Unused = l.Unused + r.Unused
    }

    let ofTranslation (translation: Translation) : TranslationCounters = 
    
        let ``new``, needsReview, final, unused = 
            { New = 1; NeedsReview = 0; Final = 0; Unused = 0 },
            { New = 0; NeedsReview = 1; Final = 0; Unused = 0 },
            { New = 0; NeedsReview = 0; Final = 1; Unused = 0 },
            { New = 0; NeedsReview = 0; Final = 0; Unused = 1 }

        let statusOf = function
            | TranslatedString.New -> ``new``
            | TranslatedString.NeedsReview _ -> needsReview
            | TranslatedString.Final _ -> final
            | TranslatedString.Unused _ -> unused

        translation.Records
        |> Seq.map ^ fun r -> statusOf r.Translated
        |> Seq.fold combine zero

[<CR(ModuleSuffix)>]
module Translation =

    [<AutoOpen>]
    module public SerializationTypes =

        type TranslationFile = {
            language: string
            records: string[][]
        }

        let deserializeTranslationRecord (strings: string[]) = 
            if strings.Length < 3 then 
                failwithf 
                    "expect a translation string to be an array of at least three strings, the state, the original, the translated. (length: %d)" 
                    strings.Length
            let original = strings.[1]
            let translatedString = 
                let translated = strings.[2]
                match strings.[0] with
                | "new" -> TranslatedString.New
                | "needs-review" -> TranslatedString.NeedsReview translated
                | "final" -> TranslatedString.Final translated
                | "unused" -> TranslatedString.Unused translated
                | unknown -> failwithf "'%s': invalid translated string state" unknown
            
            {
                Original = original
                Translated = translatedString
            }

        let stateString = function
            | TranslatedString.New -> "new"
            | TranslatedString.NeedsReview _ -> "needs-review"
            | TranslatedString.Final _ -> "final"
            | TranslatedString.Unused _ -> "unused"

        let serializeTranslatedString (record: TranslationRecord) = [| 
            stateString record.Translated
            record.Original
            string record.Translated
        |]

    let deserialize (json: string) : Translation =

        let file = 
            json
            |> JsonConvert.DeserializeObject<TranslationFile>

        let records = 
            file.records 
            |> Seq.map deserializeTranslationRecord
            |> Seq.toList

        {
            Language = Language(file.language)
            Records = records
        }
    
    let serialize (translation: Translation) : string = 

        let json = {
            language = string translation.Language
            records = 
                translation.Records
                |> Seq.map serializeTranslatedString
                |> Seq.toArray
        }

        JsonConvert.SerializeObject(json, Formatting.Indented)
        
    let createNew language originalStrings = {
        Language = language
        Records = 
            originalStrings 
            |> OriginalStrings.strings
            |> Seq.map TranslationRecord.createNew 
            |> Seq.toList
    }

    let status (translation: Translation) : string = 
        let counters = TranslationCounters.ofTranslation translation
        sprintf "[%s][%s] %s" 
            (string translation.Language) 
            (string counters) 
            (sprintf "%s/%O" TNT.Subdirectory (TranslationFilename.ofTranslation translation))

    /// Update the translation's original strings and return the translation if it changed.
    let update (strings: OriginalStrings) (translation: Translation) : Translation option = 

        let recordMap = 
            translation.Records 
            |> Seq.map ^ fun r -> r.Original, r
            |> Map.ofSeq

        let records, unusedMap =
            (recordMap, OriginalStrings.strings strings)
            ||> List.mapFold ^ fun recordMap string ->
                match recordMap.TryFind string with
                | Some r -> TranslationRecord.reuse r, recordMap |> Map.remove string
                | None -> TranslationRecord.createNew string, recordMap

        let recordsAfter = 
            unusedMap 
            |> Map.toSeq 
            |> Seq.choose (snd >> TranslationRecord.unuse)
            |> Seq.append records
            |> Seq.sortBy ^ fun r -> r.Original
            |> Seq.toList

        let recordsBefore = 
            recordMap
            |> Map.toSeq
            |> Seq.map snd
            |> Seq.toList

        if recordsAfter = recordsBefore 
        then None
        else Some { translation with Records = recordsAfter }

    /// Remove all unused entries from the translation and return the translation if
    /// it changed.
    let gc (translation: Translation) : Translation option =

        let records, changed = 
            translation.Records
            |> List.partition ^ fun r -> 
                match r.Translated with
                | TranslatedString.Unused _ -> false
                | _ -> true 
            |> fun (r, u) -> r, u <> []

        changed
        |> Option.ofBool
        |> Option.map ^ fun () -> { translation with Records = records }

module TranslationGroup = 
    
    let map f (TranslationGroup value) =
        TranslationGroup(f value)

    type TranslationGroupError = 
        | TranslationsWithTheSameLanguage of (Language * Translation list) list

    /// Groups a list of translations, checks for duplicates and inconsistent relations
    /// between assembly paths and names.
    let fromTranslations (translations: Translation list) 
        : Result<TranslationGroup, TranslationGroupError> =

        // check for duplicated languages

        let byLanguage =
            translations
            |> List.groupBy ^ fun t -> t.Language

        let duplicatedList =
            byLanguage
            |> Seq.filter (snd >> function _::_::_ -> true | _ -> false)
            |> Seq.toList

        if duplicatedList <> [] 
        then Error ^ TranslationsWithTheSameLanguage duplicatedList
        else

        byLanguage 
        |> Seq.map ^ Snd.map List.exactlyOne
        |> Map.ofSeq
        |> TranslationGroup
        |> Ok

    let translations (TranslationGroup map) =
        map
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.toList

    let hasLanguage (language: Language) (TranslationGroup(map)) : bool = 
        map.ContainsKey language

    /// Returns all the original strings in the translation group.
    let originalStrings (TranslationGroup(map)) : OriginalStrings = 
        map
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.collect ^ fun translation -> translation.Records
        |> Seq.map ^ fun record -> record.Original
        |> OriginalStrings.create

    /// Add a language to a translation group and return the new translation.
    let addLanguage (language: Language) (TranslationGroup(map) as group) : Translation option =
        if map.ContainsKey language then None else
        let strings = originalStrings group
        Some ^ Translation.createNew language strings
        