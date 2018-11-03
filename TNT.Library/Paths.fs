/// Functions that fiddle around with paths.
[<AutoOpen>]
module TNT.Library.Paths

open TNT.Model

module TNT =
    let [<Literal>] Subdirectory = ".tnt"

module Translation =

    /// The filename of the translation
    let filename (translation: Translation) : Translation filename =
        sprintf "translation-%s.json" (string translation.Language)
        |> Filename

    let FilenamePattern = GlobPattern("translation-*.json")

// Tag related to XLIFF
[<Struct>]
type XLIFF = XLIFF

module XLIFF = 

    let [<Literal>] Extension = ".xlf"
    // VisualStudio uses the dot extension to separate the identifier from the base name.
    let [<Literal>] IdentifierSeparator = "."

    let filenameForLanguage (project: ProjectName) (LanguageTag(identifier)) : XLIFF filename =
        Filename ^ string project + IdentifierSeparator + identifier + Extension

    let pattern (project: ProjectName) : GlobPattern =
        GlobPattern(string project + "*" + Extension)
    