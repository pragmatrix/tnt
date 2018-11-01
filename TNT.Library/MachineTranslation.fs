namespace TNT.Library.MachineTranslation

open TNT.Model
open TNT.Library

type TranslationReport = {
    /// The provider and its specific settings that were used.
    Provider: string
    /// The source language.
    SourceLanguage: LanguageTag
    /// The target language.
    TargetLanguage: LanguageTag
    /// All the successfully translated strings we received back and could integrate into
    /// the translation.
    Translated: int
    /// All the strings sent to the API.
    All: int
    /// The unusable translated strings we received back (for some reason, we could not
    /// map them back to the original strings sent to the API).
    Unusable: int
} with 
    override this.ToString() = 

        let prefix = 
            sprintf "%s: [%O]->[%O]" this.Provider this.SourceLanguage this.TargetLanguage

        let details =
            match () with
            | _ when this.All = this.Translated
                -> sprintf "%d translated" this.Translated
            | _ when this.Unusable = 0 
                -> sprintf "%d of %d translated" this.Translated this.All
            | _ -> sprintf "%d of %d translated, %d unused" this.Translated this.All this.Unusable

        Text.concat " " [prefix; details]

module TranslationReport =

    let zero provider (sourceLanguage, targetLanguage) = { 
        Provider = provider
        SourceLanguage = sourceLanguage
        TargetLanguage = targetLanguage
        Translated = 0
        All = 0
        Unusable = 0
    }

type TranslationResult = 
    | NoStringsToTranslate of Translation
    | TranslationUnchanged of TranslationReport * Translation
    | Translated of TranslationReport * Translation
    override this.ToString() = 
        match this with
        | NoStringsToTranslate(translation) 
            -> sprintf "No strings to translate for %s" 
                (Translation.shortStatus translation)
        | TranslationUnchanged(report, translation) 
            -> sprintf "%O %s (no changes)" 
                report (Translation.shortStatus translation)
        | Translated(report, translation) 
            -> sprintf "%O %s" 
                report (Translation.shortStatus translation)

type Translator = {
    ProviderName: string
    Translate: (LanguageTag * LanguageTag) -> string list -> (string * string) list
}

module Translate = 

    let newStrings 
        (translator: Translator) 
        (sourceLanguage: LanguageTag) 
        (translation: Translation) =
        let toTranslate = 
            translation.Records
            |> Seq.filter ^ fun r -> r.Translated = TranslatedString.New
            |> Seq.map ^ fun r -> r.Original
            |> Seq.toList

        // no need to power up the translation API if there is nothing to translate
        match toTranslate with
        | [] -> NoStringsToTranslate translation
        | toTranslate ->
        
        let resultPairs = 
            translator.Translate (sourceLanguage, translation.Language) toTranslate
            |> Map.ofList

        let newRecords, unprocessed =
            (resultPairs, translation.Records)
            ||> List.mapFold ^ fun pairs record ->
                if record.Translated = TranslatedString.New && pairs.ContainsKey record.Original then
                    { record with Translated = TranslatedString.NeedsReview pairs.[record.Original] },
                    pairs |> Map.remove record.Original
                else
                    record, pairs
    
        let report = 
            let all = toTranslate.Length
            let unusable = unprocessed.Count
            let translated = resultPairs.Count - unusable
            {
                Provider = translator.ProviderName
                SourceLanguage = sourceLanguage
                TargetLanguage = translation.Language
                Translated = translated
                All = all
                Unusable = unprocessed.Count
            }

        if newRecords <> translation.Records then
            Translated(report, { translation with Records = newRecords })
        else
            TranslationUnchanged(report, translation)

