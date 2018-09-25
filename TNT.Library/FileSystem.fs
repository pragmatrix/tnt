/// Functions that access the filesystem or screw around with paths.
[<AutoOpen>]
module TNT.Library.FileSystem

open System.IO
open System.Text
open FunToolbox.FileSystem
open TNT.Model
open TNT.Library.Paths
        
module TranslationFilenames =
    
    /// Get all translation filenames in the given directory.
    let inDirectory (directory: TranslationDirectory) : TranslationFilename list = 
        Directory.EnumerateFiles (string directory, "*.tnt")
        |> Seq.map (Path.parse >> Path.name >> TranslationFilename)
        |> Seq.toList

module Translation = 
    
    /// Load the translation at the given path.
    let load (path: Path) : Translation =
        path
        |> File.loadText Encoding.UTF8
        |> Translation.deserialize

    /// Save a translation to the given path, overwrites if a file exists there.
    let save (path: Path) (translation: Translation) =
        translation
        |> Translation.serialize
        |> fun str -> File.WriteAllText(string path, str, Encoding.UTF8)

module Translations = 

    /// Load all the translations in the given directory.    
    let loadAll (directory: TranslationDirectory) : Translation list = 

        TranslationFilenames.inDirectory directory
        |> Seq.map ^ fun fn -> 
            directory
            |> TranslationDirectory.extend fn
        |> Seq.map Translation.load
        |> Seq.toList
