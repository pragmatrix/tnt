/// Functions that access the filesystem or screw around with paths.
[<AutoOpen>]
module TNT.Library.FileSystem

open System.IO
open System.Text
open FunToolbox.FileSystem
open TNT.Model
open TNT.Library.Paths
open TNT.Library.Translation

module Sources = 

    let DefaultLanguage = Language("en-US")
    let [<Literal>] SourcesFilename = "sources.json"
    
    let path (baseDirectory: Path) : Path = 
        baseDirectory
        |> Path.extend TNT.Subdirectory
        |> Path.extend SourcesFilename

    let load (path: Path) : Sources =
        path
        |> File.loadText Encoding.UTF8
        |> Sources.deserialize

    let save (path: Path) (sources: Sources) : unit =
        sources
        |> Sources.serialize
        |> fun content -> File.saveText Encoding.UTF8 content path

    /// Extract all original strings from all sources.
    let extractOriginalStrings (baseDirectory: Path) (sources: Sources) : OriginalStrings = 
        let stringsFromSource (source: Source) : OriginalStrings = 
            match source with
            | AssemblySource (AssemblyPath path) -> 
                let fullPath = baseDirectory |> Path.extend path
                StringExtractor.extract fullPath
        sources.Sources 
        |> Seq.map stringsFromSource
        |> OriginalStrings.merge
        
module TranslationFilenames =
    
    /// Get all translation filenames in the given directory. Note: the directory does not contain
    /// the subdirectory ".tnt"
    let inDirectory (baseDirectory: Path) : TranslationFilename list = 
        let directory = baseDirectory |> Path.extend TNT.Subdirectory
        if not ^ Directory.exists directory then [] else
        Directory.EnumerateFiles (string directory, string TranslationFilename.Pattern)
        |> Seq.map (Path.parse >> Path.name >> TranslationFilename)
        |> Seq.toList

module Translation = 
    
    /// The filename of the translation
    let filename (translation: Translation) : TranslationFilename =
        TranslationFilename.ofTranslation translation

    /// The full path of the translation
    let path (directory: Path) (translation: Translation) : Path =
        let filename = filename translation
        directory 
        |> Path.extend TNT.Subdirectory 
        |> Path.extend (string filename)

    /// Load the translation at the given path.
    let load (path: Path) : Translation =
        path
        |> File.loadText Encoding.UTF8
        |> Translation.deserialize

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
            |> Path.extend TNT.Subdirectory
            |> Path.extend (string fn)
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

module XLIFFFilenames = 
    
    /// Get all the XLIFF files in the directory baseName.
    let inDirectory (directory: Path) (project: ProjectName) : XLIFFFilename list =
        Directory.EnumerateFiles (string directory, string ^ XLIFFFilename.pattern project)
        |> Seq.map (Path.parse >> Path.name >> XLIFFFilename)
        |> Seq.toList
    