module TNT.Library.MachineTranslation.Google

open TNT.Model
open TNT.Library
open Google.Cloud

let Translator = {
    ProviderName = "Google"
    Translate = fun (sourceLanguage, targetLanguage) strings ->
        use client = Translation.V2.TranslationClient.Create()
        
        let supportedTags = 
            client.ListLanguages()
            |> Seq.map ^ fun l -> LanguageTag l.Code
            |> Set.ofSeq

        let isSupported tag = 
            supportedTags |> Set.contains tag

        let tryUse (tag: LanguageTag) : LanguageTag =
            if isSupported tag then 
                tag
            elif isSupported tag.Primary then 
                tag.Primary
            else 
                failwithf "Unsupported language [%O]. Supported are: [%s]" 
                    (string tag) 
                    (supportedTags |> Seq.map string |> String.concat ",")

        let sourceLanguage, targetLanguage = 
            tryUse sourceLanguage, tryUse targetLanguage

        client.TranslateText(
            strings, 
            string targetLanguage, 
            string sourceLanguage)
        |> Seq.map ^ fun result ->
            result.OriginalText, result.TranslatedText
        |> Seq.toList
}
