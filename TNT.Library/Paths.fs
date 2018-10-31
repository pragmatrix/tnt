/// Functions that fiddle around with paths.
[<AutoOpen>]
module TNT.Library.Paths

open System.IO
open TNT.Model
open FunToolbox.FileSystem

module AssemblyFilename = 

    let ofPath (path: AssemblyPath) = 
        // note: AssemblyPath is a relative path, so we can't use
        // the FunToolbox Path which is always an absolute one.
        Path.GetFileName(string path)
        |> AssemblyFilename

module TranslationFilename =

    let ofTranslation (translation: Translation) : TranslationFilename =
        sprintf "translation-%s.json" (string translation.Language)
        |> TranslationFilename

    let Pattern = GlobPattern("translation-*.json")

module XLIFFFilename = 
    
    let [<Literal>] Extension = ".xlf"
    // VisualStudio uses the dot extension to separate the identifier from the base name.
    let [<Literal>] IdentifierSeparator = "."

    let filenameForLanguage (project: ProjectName) (Language(identifier)) : XLIFFFilename =
        XLIFFFilename ^ string project + IdentifierSeparator + identifier + Extension

    let pattern (project: ProjectName) : GlobPattern =
        GlobPattern(string project + "*" + Extension)
    