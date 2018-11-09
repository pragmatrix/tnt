/// Support functions.
[<AutoOpen>]
module TNT.Library.Translation

open Chiron
open TNT.Model

module Seq = 
    let setify seq = seq |> Seq.sort |> Seq.distinct

module OriginalStrings = 
    let format (strings: OriginalStrings) = 
        let strings = strings |> OriginalStrings.strings
        strings 
        |> List.collect ^ fun (original, contexts) ->
            [
                yield Format.prop "string" original
                for context in contexts ->
                    Format.prop "context" (string context)
            ]

module TranslationRecord = 

    let createNew original contexts = { 
        Original = original; 
        Translated = TranslatedString.New 
        Contexts = contexts
        Notes = []
    }

    let unuse (record: TranslationRecord) : TranslationRecord option = 
        match record.Translated with
        | TranslatedString.New
            -> None
        | TranslatedString.NeedsReview str 
        | TranslatedString.Final str
        | TranslatedString.Unused str
            -> Some ^ { record with Translated = TranslatedString.Unused str }

    /// Update an existing translation record string.
    let update 
        (existing: TranslationRecord) 
        (newContexts: LogicalContext list) : TranslationRecord = 
        match existing.Translated with
        | TranslatedString.New _
        | TranslatedString.NeedsReview _
        | TranslatedString.Final _ -> 
            { existing with Contexts = newContexts }
        | TranslatedString.Unused str -> 
            { existing with 
                Translated = TranslatedString.NeedsReview str 
                Contexts = newContexts
            }

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
    module Serialization =

        let str = function
            | String str -> str
            | unexpected -> failwithf "expected a string, seen: %A" unexpected

        let array = function
            | Array json -> json
            | unexpected -> failwithf "expected an array, seen: %A" unexpected

        let deserializeTranslationRecord (record: Json) = 
            match record with
            | Array arr when arr.Length >= 3 ->
                let original = str arr.[1]
                let translatedString = 
                    let translated = str arr.[2]
                    match str arr.[0] with
                    | "new" -> TranslatedString.New
                    | "needs-review" -> TranslatedString.NeedsReview translated
                    | "final" -> TranslatedString.Final translated
                    | "unused" -> TranslatedString.Unused translated
                    | unknown -> failwithf "'%s': invalid translated string state" unknown

                let contexts = 
                    if arr.Length <= 3 then [] else
                    match arr.[3] with
                    | Array arr 
                        -> arr |> List.map (str >> LogicalContext)
                    | unexpected 
                        -> failwithf "expected an array of strings at the fourth position inside a language record, seen: %A" unexpected

                let notes =
                    if arr.Length <= 4 then [] else
                    match arr.[4] with
                    | Array arr
                        -> arr |> List.map str
                    | unexpected
                        -> failwithf "expected an array of strings at the fifth position inside a language record, seen: %A" unexpected

                {
                    Original = original
                    Translated = translatedString
                    Contexts = contexts
                    Notes = notes
                }

            | unexpected ->
                failwithf 
                    "expect a translation record to be an array of at least three things, the original, the state, and the translated string, seen: %A" 
                    unexpected

        let stateString = function
            | TranslatedString.New -> "new"
            | TranslatedString.NeedsReview _ -> "needs-review"
            | TranslatedString.Final _ -> "final"
            | TranslatedString.Unused _ -> "unused"

        let serializeRecord (record: TranslationRecord) : Json = Array [
            yield String ^ stateString record.Translated
            yield String record.Original
            yield String ^ string record.Translated
            yield Array (record.Contexts |> List.map (string >> String))
            if record.Notes <> [] then
                yield Array (record.Notes |> List.map String)
        ]

    let destructure (f: Json<'a>) (js: Json) = 
        match f js with
        | Value v, _ -> v
        | _ -> failwith "parse error"

    let deserialize (js: string) : Translation =

        let file = Json.parse js

        let (language : string, records : Json) = 
            file 
            |> destructure ^ json {
                let! language = Json.read "language"
                let! records = Json.read "records"
                return (language, records)
            }

        let records = 
            records
            |> array
            |> Seq.map deserializeTranslationRecord
            |> Seq.toList

        {
            Language = LanguageTag(language)
            Records = records
        }
    
    let serialize (translation: Translation) : string = 

        let json = Object ^ Map.ofList [
            "language", String ^ string translation.Language
            "records",
                translation.Records
                |> Seq.map serializeRecord
                |> Seq.toList
                |> Array
        ]

        json 
        |> Json.formatWith JsonFormattingOptions.Pretty

        
    let createNew language originalStrings = {
        Language = language
        Records = 
            originalStrings 
            |> OriginalStrings.strings
            |> Seq.map ^ uncurry TranslationRecord.createNew 
            |> Seq.toList
    }

    let shortStatus (translation: Translation) : string = 
        let counters = TranslationCounters.ofTranslation translation
        sprintf "%s%s" 
            (translation.Language.Formatted) 
            (counters.Formatted) 

    let status (translation: Translation) : string = 
        Text.concat " " [
            shortStatus translation 
            SystemCultures.tryGetName (translation.Language) 
                |> Option.map ^ fun cn -> cn.Formatted
                |> Option.defaultValue ""
            (sprintf "%O" 
                (TNT.Subdirectory 
                |> RPath.extendF (Translation.filename translation)))
        ]

    /// Update the translation's original strings and return the translation if it changed.
    let update (strings: OriginalStrings) (translation: Translation) : Translation option = 

        let recordMap = 
            translation.Records 
            |> Seq.map ^ fun r -> r.Original, r
            |> Map.ofSeq

        let records, unusedMap =
            (recordMap, OriginalStrings.strings strings)
            ||> List.mapFold ^ fun recordMap (string, contexts) ->
                match recordMap.TryFind string with
                | Some existing 
                    -> TranslationRecord.update existing contexts, recordMap |> Map.remove string
                | None 
                    -> TranslationRecord.createNew string contexts, recordMap

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
        | TranslationsWithTheSameLanguage of (LanguageTag * Translation list) list

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
        then Result.Error ^ TranslationsWithTheSameLanguage duplicatedList
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

    let hasLanguage (language: LanguageTag) (TranslationGroup(map)) : bool = 
        map.ContainsKey language

    /// Returns all the original strings in the translation group.
    let originalStrings (TranslationGroup(map)) : OriginalStrings = 
        map
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.collect ^ fun translation -> translation.Records
        |> Seq.map ^ fun record -> record.Original, record.Contexts
        |> OriginalStrings.create

    /// Add a language to a translation group and return the new translation.
    let addLanguage (language: LanguageTag) (TranslationGroup(map) as group) : Translation option =
        if map.ContainsKey language then None else
        let strings = originalStrings group
        Some ^ Translation.createNew language strings
        
module TranslationContent =

    let serialize (content: TranslationContent) =
        
        let json = Array [
            for pair in content.Pairs ->
                Array [ String ^ fst pair; String ^ snd pair ]
        ]

        json |> Json.formatWith JsonFormattingOptions.Compact

    let fromTranslation (translation: Translation) : TranslationContent = 
    
        let recordToPair (record: TranslationRecord) : (string * string) option =
            match record.Translated with
            | TranslatedString.New
            | TranslatedString.Unused _
                -> None
            | TranslatedString.NeedsReview str 
            | TranslatedString.Final str 
                -> Some (record.Original, str)

        {
            Language = translation.Language
            Pairs = 
                translation.Records
                |> List.choose recordToPair
        }