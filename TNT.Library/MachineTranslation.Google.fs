module TNT.Library.MachineTranslation.Google

open System.IO
open System.Json
open TNT.Model
open TNT.Library
open Google.Cloud.Translate.V3

let Translator = {
    ProviderName = "Google"
    Translate = fun (sourceLanguage, targetLanguage) strings ->

        let parent = 
            let AppCredentialsEnv = "GOOGLE_APPLICATION_CREDENTIALS"
            let Docs = "https://cloud.google.com/translate/docs/setup#using_the_service_account_key_file_in_your_environment"
            let credentials_path = System.Environment.GetEnvironmentVariable(AppCredentialsEnv)
            if credentials_path = null then
                failwithf "To use Google Cloud Translation Services, the %s environment variable must be set. For more information: %s" AppCredentialsEnv Docs
            if not ^ File.Exists credentials_path then
                failwithf "The %s environment variable points to '%s', but this file does not exist." AppCredentialsEnv credentials_path
            let credentials = 
                File.ReadAllText credentials_path
                |> JsonValue.Parse
            let projectId : string = 
                credentials.["project_id"] 
                |> JsonValue.op_Implicit
            "projects/" + projectId

        let client = TranslationServiceClient.Create()
        
        let supportedTargetLanguageTags : Set<LanguageTag> = 
            let request = GetSupportedLanguagesRequest(Parent = parent)
            let languages = client.GetSupportedLanguages(request)
            languages.Languages
            |> Seq.filter ^ fun l -> l.SupportTarget
            |> Seq.map ^ fun l -> LanguageTag l.LanguageCode
            |> Set.ofSeq

        let isSupported tag = 
            supportedTargetLanguageTags |> Set.contains tag

        let tryUse (tag: LanguageTag) : LanguageTag =
            match () with
            | _ when isSupported tag -> tag
            | _ when isSupported tag.Primary -> tag.Primary
            | _ ->
                failwithf "Unsupported language %s. Supported are: [%s]" 
                    tag.Formatted 
                    (supportedTargetLanguageTags |> Seq.map string |> String.concat ",")

        let sourceLanguage, targetLanguage = 
            tryUse sourceLanguage, tryUse targetLanguage

        let request = 
            TranslateTextRequest(
                Parent = parent,
                SourceLanguageCode = string sourceLanguage,
                TargetLanguageCode = string targetLanguage,
                MimeType = "text/plain")
        request.Contents.AddRange(strings)
        let response = client.TranslateText(request)
        let strings = Array.ofList strings
        let translations = response.Translations
        if translations.Count <> strings.Length then
            failwithf "Expected %d translations to be returned, received %d instead." strings.Length translations.Count
        translations
        |> Seq.mapi ^ fun i result ->
            strings.[i], result.TranslatedText
        |> Seq.toList
}
