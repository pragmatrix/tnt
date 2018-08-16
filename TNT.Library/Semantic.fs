module TNT.Semantic

open System.IO
open TNT.Model

module AssemblyPath = 
    
    let filename (AssemblyPath(path)) = 
        AssemblyFilename(Path.GetFileName(path))

module TranslationId =

    let filename (TranslationId(languageCode, assemblyPath)) : TranslationFilename =
        let assemblyFilename = AssemblyPath.filename assemblyPath
        sprintf "%s-%s.tnt" (string assemblyFilename) (string languageCode)
        |> TranslationFilename
