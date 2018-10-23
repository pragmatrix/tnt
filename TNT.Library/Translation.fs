/// Support functions.
[<AutoOpen>]
module TNT.Library.Translation

open Newtonsoft.Json
open TNT.Model

[<CR(ModuleSuffix)>]
module Translation =

    [<AutoOpen>]
    module public SerializationTypes =

        type TranslationFile = {
            assembly: string
            language: string
            strings: string[][]
        }

        let deserializeTranslatedString (strings: string[]) = 
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

        let stateString (translated: TranslatedString) = 
            match translated with
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

        let translations = 
            file.strings 
            |> Seq.map deserializeTranslatedString
            |> Seq.toList

        Translation(
            TranslationId(
                AssemblyPath(file.assembly), 
                LanguageIdentifier(file.language))
            , translations)
    
    let serialize (Translation(TranslationId(assembly, language), strings)) : string = 

        let json = {
            assembly = string assembly
            language = string language
            strings = 
                strings
                |> Seq.map serializeTranslatedString
                |> Seq.toArray
        }

        JsonConvert.SerializeObject(json, Formatting.Indented)
        
    let id (Translation(id, _)) : TranslationId = id
    let records (Translation(_, records)) = records
    let language = id >> function TranslationId(identifier = identifier) -> identifier
    let assemblyPath = id >> function TranslationId(path = path) -> path
    let assemblyFilename = assemblyPath >> AssemblyFilename.ofPath
    let createNew id strings = 
        let translatedStrings = strings |> List.map TranslationRecord.createNew
        Translation(id, translatedStrings)

module TranslationStatus =

    let combine (l: TranslationStatus) (r: TranslationStatus) = {
        New = l.New + r.New
        NeedsReview = l.NeedsReview + r.NeedsReview
        Final = l.Final + r.Final
        Unused = l.Unused + r.Unused
    }

    let ofTranslation (translation: Translation) : TranslationStatus = 
    
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

        translation
        |> Translation.records
        |> Seq.map ^ fun r -> statusOf r.Translated
        |> Seq.reduce combine

module Translations = 

    /// All the ids (sorted and duplicates removed) from a list of translations.
    let ids (translations: Translation list) =
        let id (Translation(id = id)) = id

        translations
        |> Seq.map id
        |> Seq.sort
        |> Seq.distinct
        |> Seq.toList

module TranslationIds =
    
    /// All the assemblies (sorted and duplicates removed) from the list of ids.
    let assemblyPaths (translations: TranslationId list) =
        let path (TranslationId(path = path)) = path

        translations
        |> Seq.map path
        |> Seq.sort
        |> Seq.distinct
        |> Seq.toList

module TranslationSet = 

    let map f (TranslationSet(assembly, set)) = 
        TranslationSet(f (assembly, set))

    let assemblyPath (TranslationSet(path, _)) = path

    let translations (TranslationSet(_, set)) = 
        set 
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.toList

    let translation (language: LanguageIdentifier) (TranslationSet(_, set)) = 
        set 
        |> Map.tryFind language

    /// Add or update a translation in a translation set.
    let addOrUpdate (translation: Translation) (TranslationSet(path, set)) = 
        let language = Translation.language translation
        let translationPath = Translation.assemblyPath translation
        if translationPath <> path then
            failwithf 
                "unexpected assembly path for '%s' translation '%s', should be '%s'" 
                (string language)
                (string translationPath)
                (string path)
        else
        let newSet = set |> Map.add language translation
        TranslationSet(path, newSet)

    type TSError = 
        | DifferentAssemblyPaths of AssemblyPath list

    // Create a translation set from a list of translations.
    // Each of the translations must point to the Same assembly.
    let fromTranslations (translations: Translation list) : TranslationSet =

        let byPath =
            translations
            |> List.groupBy ^ Translation.assemblyPath

        if byPath.Length <> 1 then
            failwithf "internal error, unexpected assembly path differences: %A" (byPath |> List.map fst)
        else
        let path = 
            byPath
            |> List.head
            |> fst

        let map = 
            translations
            |> Seq.map ^ fun t -> Translation.language t, t
            |> Map.ofSeq 

        TranslationSet(path, map)

module TranslationGroup = 
    
    let map f (TranslationGroup value) =
        TranslationGroup(f value)

    type TranslationGroupError = 
        | AssemblyPathsWithTheSameFilename of (AssemblyFilename * AssemblyPath list) list
        | TranslationsWithTheSameLanguage of ((AssemblyFilename * LanguageIdentifier) * Translation list) list

    /// Groups a list of translations, checks for duplicates and inconsistent relations
    /// between assembly paths and names.
    let fromTranslations (translations: Translation list) 
        : Result<TranslationGroup, TranslationGroupError> =

        // find different AssemblyPaths that point to the same filename.
        let overlappingAssemblies =
            translations
            |> Translations.ids
            |> TranslationIds.assemblyPaths
            |> List.groupBy ^ AssemblyFilename.ofPath
            |> Seq.filter (snd >> function _::_::_ -> true | _ -> false)
            |> Seq.toList

        if overlappingAssemblies <> [] 
        then Error ^ AssemblyPathsWithTheSameFilename overlappingAssemblies
        else

        // check for duplicated languages

        let duplicatedList = 
            translations
            |> List.groupBy (fun t -> Translation.assemblyFilename t, Translation.language t)
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
        (filename: AssemblyFilename, language: LanguageIdentifier) 
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
    