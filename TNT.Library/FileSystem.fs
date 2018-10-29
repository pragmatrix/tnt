/// Functions that access the filesystem or screw around with paths.
[<AutoOpen>]
module TNT.Library.FileSystem

open System.IO
open System.Text
open FunToolbox.FileSystem
open TNT.Model
open TNT.Library.Paths
open TNT.Library.Translation

let [<Literal>] TranslationSubdirectory = ".tnt"
        
module TranslationFilenames =
    
    /// Get all translation filenames in the given directory. Note: the directory does not contain
    /// the subdirectory ".tnt"
    let inDirectory (directory: Path) : TranslationFilename list = 
        let directory = directory |> Path.extend TranslationSubdirectory
        if not ^ Directory.exists directory then [] else
        Directory.EnumerateFiles (string directory, "*.tnt")
        |> Seq.map (Path.parse >> Path.name >> TranslationFilename)
        |> Seq.toList

module Translation = 
    
    /// Load the translation at the given path.
    let load (path: Path) : Translation =
        path
        |> File.loadText Encoding.UTF8
        |> Translation.deserialize

    /// The filename of the translation
    let filename (translation: Translation) : TranslationFilename =
        TranslationFilename.ofId (Translation.id translation)

    /// The full path of the translation
    let path (directory: Path) (translation: Translation) : Path =
        let filename = filename translation
        directory 
        |> Path.extend TranslationSubdirectory 
        |> Path.extend (string filename)

    /// Save a translation to the given path, overwrites if a file exists there.
    let save (path: Path) (translation: Translation) =
        translation
        |> Translation.serialize
        |> fun str -> 
            File.WriteAllText(string path, str, Encoding.UTF8)

module Translations = 

    /// Load all the translations in the given directory.
    let loadAll (directory: Path) : Translation list = 

        // Note that we can't support filenames to diverge from the translation id's
        // to maintain the export / import integrity.
        let checkFilename filename translation = 
            let expected = string ^ Translation.filename translation
            if filename <> expected then
                failwithf "can't load translation with filename '%s', it must be '%s', did you rename it?" filename expected

        TranslationFilenames.inDirectory directory
        |> Seq.map ^ fun fn -> 
            directory
            |> Path.extend TranslationSubdirectory
            |> TranslationDirectory.extend fn
        |> Seq.map ^ fun path ->
            let translation = Translation.load path
            checkFilename (Path.name path) translation
            translation
        |> Seq.toList

    let saveAll (directory: Path) (translations: Translation list) =
        translations
        |> Seq.map ^ fun t -> Translation.path directory t, t
        |> Seq.iter ^ uncurry Translation.save

module TranslationGroup = 
    
    let load (directory: Path) = 
        Translations.loadAll directory
        |> TranslationGroup.fromTranslations

