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

        let batchesOf n =
            Seq.mapi (fun i v -> i / n, v) >>
            Seq.groupBy fst >>
            Seq.map snd >>
            Seq.map (Seq.map snd)

        let isSupported tag = 
            supportedTags |> Set.contains tag

        let tryUse (tag: LanguageTag) : LanguageTag =
            if isSupported tag then 
                tag
            elif isSupported tag.Primary then 
                tag.Primary
            else 
                failwithf "Unsupported language %s. Supported are: [%s]" 
                    tag.Formatted 
                    (supportedTags |> Seq.map string |> String.concat ",")

        let sourceLanguage, targetLanguage = 
            tryUse sourceLanguage, tryUse targetLanguage

        let batches = strings |> batchesOf 64
                
        let results = batches |> Seq.map(fun(b) ->
                            client.TranslateText(
                                b, 
                                string targetLanguage, 
                                string sourceLanguage)
                            |> Seq.map ^ fun result ->
                                result.OriginalText, result.TranslatedText)


        results |> Seq.collect(fun (a) -> a) |> Seq.toList
}
