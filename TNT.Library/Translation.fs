/// Support functions.
[<AutoOpen>]
module TNT.Library.Translation

open Newtonsoft.Json
open TNT.Model

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
    let createNew assembly language originalStrings = {
        Assembly = assembly
        Language = language
        Records = originalStrings |> List.map TranslationRecord.createNew
    }

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

        translation.Records
        |> Seq.map ^ fun r -> statusOf r.Translated
        |> Seq.reduce combine

module Translations = 

    /// All the ids (sorted and duplicates removed) from a list of translations.
    let ids (translations: Translation list) : TranslationId list =
        translations
        |> Seq.map Translation.id
        |> Seq.sort
        |> Seq.distinct
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
    