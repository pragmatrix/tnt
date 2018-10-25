﻿/// Support functions.
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
        |> Seq.reduce combine

[<CR(ModuleSuffix)>]
module Translation =

    [<AutoOpen>]
    module public SerializationTypes =

        type Assembly = {
            path: string
            language: string
        }

        type TranslationFile = {
            assembly: Assembly
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

        let assembly = file.assembly

        {
            Assembly = { 
                Path = AssemblyPath(assembly.path)
                Language = Language(assembly.language) 
            }
            Language = Language(file.language)
            Records = records
        }
    
    let serialize (translation: Translation) : string = 

        let assembly = translation.Assembly

        let json = {
            assembly = { 
                path = string assembly
                language = string assembly.Language
            }
            language = string translation.Language
            records = 
                translation.Records
                |> Seq.map serializeTranslatedString
                |> Seq.toArray
        }

        JsonConvert.SerializeObject(json, Formatting.Indented)
        
    let id (translation: Translation) = 
        TranslationId(translation.Assembly.Path |> AssemblyFilename.ofPath, translation.Language)
    let assemblyFilename (translation: Translation) = 
        id translation |> function TranslationId(filename, _) -> filename
    let createNew language originalStrings = {
        Assembly = OriginalStrings.assembly originalStrings
        Language = language
        Records = 
            originalStrings 
            |> OriginalStrings.strings
            |> Seq.map TranslationRecord.createNew 
            |> Seq.toList
    }

    let status (translation: Translation) : string = 
        let (TranslationId(filename, lang)) = id translation
        let counters = TranslationCounters.ofTranslation translation
        sprintf "[%s:%s][%s] %s" (string lang) (string filename) (string counters) (string translation.Assembly.Path)

    /// Update the translation's original strings and return the translation if it changed.
    let update (strings: OriginalStrings) (translation: Translation) : Translation option = 
        assert (OriginalStrings.assembly strings = translation.Assembly)

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

module Translations = 

    /// All the ids (sorted and duplicates removed) from a list of translations.
    let ids (translations: Translation list) : TranslationId list =
        translations
        |> Seq.map Translation.id
        |> Seq.setify
        |> Seq.toList
    
module TranslationSet = 

    let map f (TranslationSet(assembly, set)) = 
        TranslationSet(f (assembly, set))

    let assembly (TranslationSet(assembly, _)) = assembly

    let translations (TranslationSet(_, set)) = 
        set 
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.toList

    let languages (TranslationSet(_, set)) = 
        set
        |> Map.toSeq
        |> Seq.map fst
        |> Seq.toList

    let translation (language: Language) (TranslationSet(_, set)) = 
        set 
        |> Map.tryFind language

    /// Add or update a translation in a translation set.
    let addOrUpdate (translation: Translation) (TranslationSet(assembly, set)) = 
        let language = translation.Language
        let translationAssembly = translation.Assembly
        if translationAssembly <> assembly then
            failwithf 
                "unexpected assembly for language '%s' translation '%s', should be '%s'" 
                (string language)
                (string translationAssembly)
                (string assembly)
        else
        let newSet = set |> Map.add language translation
        TranslationSet(assembly, newSet)

    // Create a translation set from a list of translations.
    // Each of the translations must point to the Same assembly.
    let fromTranslations (translations: Translation list) : TranslationSet =

        let byPath =
            translations
            |> List.groupBy ^ fun t -> t.Assembly

        if byPath.Length <> 1 then
            failwithf "internal error, unexpected assembly path differences: %A" (byPath |> List.map fst)
        else
        let path = 
            byPath
            |> List.head
            |> fst

        let map = 
            translations
            |> Seq.map ^ fun t -> t.Language, t
            |> Map.ofSeq 

        TranslationSet(path, map)

    /// All the original strings that appear in all translations of the set, sorted and without duplicates.
    let originalStrings (set: TranslationSet) : OriginalStrings =
        set
        |> translations
        |> Seq.collect ^ fun t -> t.Records
        |> Seq.map ^ fun r -> r.Original
        |> OriginalStrings.create (assembly set)

    /// Update the original strings in the translation set and return the translations that changed.
    let update (strings: OriginalStrings) (set: TranslationSet) : Translation list =
        set
        |> translations
        |> Seq.choose ^ Translation.update strings
        |> Seq.toList

    /// Garbage collect the unused strings and return the translations that changed.
    let gc (set: TranslationSet) : Translation list =
        set
        |> translations
        |> Seq.choose ^ Translation.gc
        |> Seq.toList

module TranslationGroup = 
    
    let map f (TranslationGroup value) =
        TranslationGroup(f value)

    type TranslationGroupError = 
        | AssemblyPathsWithTheSameFilename of (AssemblyFilename * AssemblyPath list) list
        | TranslationsWithTheSameLanguage of ((AssemblyFilename * Language) * Translation list) list

    /// Groups a list of translations, checks for duplicates and inconsistent relations
    /// between assembly paths and names.
    let fromTranslations (translations: Translation list) 
        : Result<TranslationGroup, TranslationGroupError> =

        // find different AssemblyPaths that point to the same filename.
        let overlappingAssemblies =
            translations
            |> List.map ^ fun t -> t.Assembly.Path
            |> List.groupBy ^ AssemblyFilename.ofPath
            |> Seq.filter (snd >> function _::_::_ -> true | _ -> false)
            |> Seq.toList

        if overlappingAssemblies <> [] 
        then Error ^ AssemblyPathsWithTheSameFilename overlappingAssemblies
        else

        // check for duplicated languages

        let duplicatedList =
            translations
            |> List.groupBy ^ fun t -> Translation.assemblyFilename t, t.Language
            |> Seq.filter (snd >> function _::_::_ -> true | _ -> false)
            |> Seq.toList

        if duplicatedList <> [] 
        then Error ^ TranslationsWithTheSameLanguage duplicatedList
        else

        // group

        let grouped = 
            translations
            |> List.groupBy ^ Translation.assemblyFilename
            |> Seq.map ^ fun (fn, translations) ->
                fn, TranslationSet.fromTranslations translations
            |> Map.ofSeq

        Ok ^ TranslationGroup(grouped)

    /// Try get the TranslationSet with the given AssemblyFilename
    let set (filename: AssemblyFilename) (TranslationGroup(map)) : TranslationSet option = 
        map.TryFind filename

    /// Return a translation if one is found for the given filename / language combination.
    let translation 
        (filename: AssemblyFilename, language: Language) 
        (group: TranslationGroup) = 
        group 
        |> set filename
        |> Option.bind ^ TranslationSet.translation language

    /// Get all sets.
    let sets (TranslationGroup(map)) = 
        map
        |> Map.toSeq 
        |> Seq.map snd 
        |> Seq.toList

    let translations group =
        group
        |> sets
        |> List.collect TranslationSet.translations
    
    /// Add a language to a translation group and return the new translations.
    let addLanguage (language: Language) (group: TranslationGroup) : Translation list =
        [
            for set in sets group do
                let languages = TranslationSet.languages set
                if not ^ Seq.contains language languages then
                    yield 
                        set 
                        |> TranslationSet.originalStrings
                        |> Translation.createNew language
        ]

            
        