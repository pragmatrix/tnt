/// Functions that fiddle around with paths.
[<AutoOpen>]
module TNT.Library.Paths

open System.IO
open TNT.Model
open FunToolbox.FileSystem

module TranslationDirectory = 

    let extend (file: TranslationFilename) (dir: Path) =
        dir 
        |> Path.extend (string file)

module AssemblyFilename = 

    let ofPath (path: AssemblyPath) = 
        // note: AssemblyPath is a relative path, so we can't use
        // the FunToolbox Path which is always an absolute one.
        Path.GetFileName(string path)
        |> AssemblyFilename

module TranslationFilename =

    let ofId (TranslationId(assemblyFilename, language)) : TranslationFilename =
        sprintf "%s-%s.tnt" (string assemblyFilename) (string language)
        |> TranslationFilename

module XLIFFBaseName = 
    
    let [<Literal>] FileExtension = ".xlf"
    // VisualStudio uses the dot extension to separate the identifier from the base name.
    let [<Literal>] IdentifierSeparator = "."

    let filePathForLanguage (Language(identifier)) (directory: Path) (XLIFFBaseName(baseName)) =
        directory
        |> Path.extend (baseName + IdentifierSeparator + identifier + FileExtension)