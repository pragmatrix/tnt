/// Functions that fiddle around with paths.
[<AutoOpen>]
module TNT.Library.Paths

open System.IO
open TNT.Model
open FunToolbox.FileSystem

/// Absolute or relative path. Toolbox candidate.
type ARPath =
    | AbsolutePath of Path
    | RelativePath of string
    override this.ToString() = 
        match this with
        | AbsolutePath p -> string p
        | RelativePath p -> p

module ARPath =

    /// Returns an absolute path be prepending root to it if it's relative.
    let rooted (root: Path) = function
        | AbsolutePath abs -> abs
        | RelativePath rel -> root |> Path.extend rel

    let extend (rel: ARPath) (parent: ARPath) =
        match parent, rel with
        | RelativePath parent, RelativePath rel 
            -> RelativePath (Path.Combine(parent, rel))
        | AbsolutePath path, RelativePath rel 
            -> AbsolutePath (path |> Path.extend rel)
        | _, AbsolutePath path 
            -> AbsolutePath path

    let ofString (path: string) = 
        if Path.IsPathRooted(path)
        then AbsolutePath ^ Path.parse path
        else RelativePath path

module AssemblyFilename = 

    let ofPath (path: AssemblyPath) = 
        // note: AssemblyPath is a relative path, so we can't use
        // the FunToolbox Path which is always an absolute one.
        Path.GetFileName(string path)
        |> AssemblyFilename

module TNT =
    let [<Literal>] Subdirectory = ".tnt"

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
    