module TNT.Library.MachineTranslation.Google

open TNT.Library
open Google.Cloud

let Translator = {
    ProviderName = "Google"
    Translate = fun (sourceLanguage, targetLanguage) strings ->
        use client = Translation.V2.TranslationClient.Create()
        client.TranslateText(
            strings, 
            string targetLanguage, 
            string sourceLanguage)
        |> Seq.map ^ fun result ->
            result.OriginalText, result.TranslatedText
        |> Seq.toList
}
