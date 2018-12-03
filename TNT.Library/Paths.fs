/// Functions that fiddle around with paths.
[<AutoOpen>]
module TNT.Library.Paths

open TNT.Model

module TNT =
    let Subdirectory = RPath ".tnt"
    let ContentSubdirectory = RPath ".tnt-content"

module Translation =

    let filenameOfLanguage (language: LanguageTag) : Translation filename =
        sprintf "translation-%s.json" (string language)
        |> Filename

    /// The filename of the translation
    let filename (translation: Translation) : Translation filename =
        filenameOfLanguage translation.Language

    let FilenamePattern = GlobPattern("translation-*.json")

    let languageOf (filename: Translation filename) =
        match string filename with
        | Regex.Match(@"^translation-(.+)\.json$") [tag] 
            -> Some (LanguageTag(tag))
        | _ -> None

module TranslationContent =

    let FilenamePattern = GlobPattern("*.tnt")

    let filenameOfLanguage (language: LanguageTag) : TranslationContent filename =
        sprintf "%s.tnt" (string language)
        |> Filename

    let filename (content: TranslationContent) : TranslationContent filename = 
        filenameOfLanguage content.Language

    let languageOf (filename: TranslationContent filename) =
        match string filename with
        | Regex.Match(@"^(.+)\.json$") [tag] 
            -> Some (LanguageTag(tag))
        | _ -> None
