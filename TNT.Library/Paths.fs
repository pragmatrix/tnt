/// Functions that fiddle around with paths.
[<AutoOpen>]
module TNT.Library.Paths

open System.IO
open TNT.Model
open FunToolbox.FileSystem

module TranslationDirectory = 

    let ofPath (path: Path) = 
        TranslationDirectory(string path)

    let toPath (dir: TranslationDirectory) = 
        Path.parse (string dir)

    let extend (file: TranslationFilename) (dir: TranslationDirectory) =
        dir 
        |> toPath
        |> Path.extend (string file)

module AssemblyFilename = 

    let ofPath (path: AssemblyPath) = 
        // note: AssemblyPath is a relative path, so we can't use
        // the FunToolbox Path which is always an absolute one.
        Path.GetFileName(string path)
        |> AssemblyFilename

module TranslationFilename =

    let ofId (TranslationId(assemblyPath, language)) : TranslationFilename =
        let assemblyFilename = AssemblyFilename.ofPath assemblyPath
        sprintf "%s-%s.tnt" (string assemblyFilename) (string language)
        |> TranslationFilename
