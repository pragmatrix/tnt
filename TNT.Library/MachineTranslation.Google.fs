module TNT.Library.MachineTranslation.Google

open System.IO
open System.Json
open TNT.Model
open TNT.Library
open Google.Cloud.Translate.V3

// from: https://cloud.google.com/translate/quotas#content
let [<Literal>] RecommendedMaxCodePointsPerRequest = 5000;

let internal createBatches (maxCodePoints: int) (strings: string list) : (string list list) =
    assert(maxCodePoints > 0)
    
    // Lazily count the code points in a sequence of strings.
    let countCodePoints (strings: string seq) : uint64 seq =
        (0UL, strings) 
        ||> Seq.scan ^ fun cnt str -> cnt + uint64 str.Length

    let splitAtMaxCodePointsReached (strings: string list) : string list * string list =
        let nextChunkLength = 
            countCodePoints strings
            |> Seq.tryFindIndex ^ fun points -> points >= uint64 maxCodePoints
            |> Option.defaultWith ^ fun () -> Seq.length strings
        strings 
        |> List.splitAt nextChunkLength

    let rec genBatches (chunks: string list list) (pending: string list) : string list list = 
        match splitAtMaxCodePointsReached pending with
        | [], [] 
            -> List.rev chunks
        | [], _ 
            // a consumption of at least one string must be guaranteed, even if it exceeds the max code points.
            -> failwith "internal error"
        | chunk, pending 
            -> genBatches (chunk :: chunks) pending

    genBatches [] strings

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

        let tryTranslate (strings: string list) : Result<(string * string) list, exn> =
            let request = 
                TranslateTextRequest(
                    Parent = parent,
                    SourceLanguageCode = string sourceLanguage,
                    TargetLanguageCode = string targetLanguage,
                    MimeType = "text/plain")
            request.Contents.AddRange(strings)
            let response = 
                try Ok ^ client.TranslateText(request)
                with e -> Error e
            match response with
            | Error e -> Error e
            | Ok response ->
            let strings = Array.ofList strings
            let translations = response.Translations
            if translations.Count <> strings.Length then
                Error ^ exn ^ sprintf "Expected %d translations to be returned, received %d instead." 
                    strings.Length translations.Count
            else
            translations
            |> Seq.mapi ^ fun i result ->
                strings.[i], result.TranslatedText
            |> Seq.toList
            |> Ok

        let rec translateBatches (results: (string * string) list list) (batches: string list list) 
            : (string * string) list list * exn option =
            match batches with 
            | [] -> results, None
            | batch :: batches ->
            match tryTranslate batch with
            | Error e 
                -> results, Some e
            | Ok result 
                -> translateBatches (result :: results) batches

        strings
        |> createBatches RecommendedMaxCodePointsPerRequest
        |> translateBatches []
        |> fun (results, e) 
            -> Seq.rev results |> Seq.collect id |> Seq.toList, e
}
