/// Functions that access the filesystem or screw around with paths.
[<AutoOpen>]
module TNT.Library.FileSystem

open System.IO
open System.Text
open FunToolbox.FileSystem
open TNT.Model
open TNT.Library

module Sources = 

    let DefaultLanguage = LanguageTag("en-US")
    let SourcesFilename : Sources filename = Filename "sources.json"
    
    /// Relative path to the source file.
    let path : Sources rpath = 
        TNT.Subdirectory
        |> RPath.extend (SourcesFilename |> Filename.toPath)

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
            | AssemblySource path -> 
                let fullPath = baseDirectory |> Path.extend path
                StringExtractor.extract fullPath
        sources.Sources 
        |> Seq.map stringsFromSource
        |> OriginalStrings.merge
            
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
        |> fun str -> File.saveText Encoding.UTF8 str path

module Translations = 

    /// Get all translation filenames in the given directory. Note: the directory does not contain
    /// the subdirectory ".tnt"
    let scan (baseDirectory: Path) : Translation filename list = 
        let directory = baseDirectory |> Path.extend TNT.Subdirectory
        if not ^ Directory.exists directory then [] else
        Directory.EnumerateFiles (string directory, string Translation.FilenamePattern)
        |> Seq.map (Path.parse >> Path.name >> Filename)
        |> Seq.toList

    /// Load all the translations in the given directory.
    let loadAll (directory: Path) : Translation list = 

        // Note that we can't support filenames to diverge from the translation id's
        // to maintain the export / import integrity.
        let checkFilename filename translation = 
            let expected = string ^ Translation.filename translation
            if filename <> expected then
                failwithf "can't load translation with filename '%s', it must be '%s', did you rename it?" filename expected

        scan directory
        |> Seq.map ^ fun fn -> 
            directory
            |> Path.extend TNT.Subdirectory
            |> Path.extendF fn
        |> Seq.map ^ fun path ->
            let translation = Translation.load path
            checkFilename (Path.name path) translation
            translation
        |> Seq.toList

module TranslationContent = 
    
    let save (path: Path) (content: TranslationContent) =
        content 
        |> TranslationContent.serialize    
        |> fun content -> File.saveText Encoding.UTF8 content path

module TranslationContents = 
    
    let scan (baseDirectory: Path) : TranslationContent filename list = 
        let directory = baseDirectory |> Path.extend TNT.ContentSubdirectory
        if not ^ Directory.exists directory then [] else
        Directory.EnumerateFiles (string directory, string TranslationContent.FilenamePattern)
        |> Seq.map (Path.parse >> Path.name >> Filename)
        |> Seq.toList

module TranslationGroup = 
    
    let load (directory: Path) = 
        Translations.loadAll directory
        |> TranslationGroup.fromTranslations

module XLIFFFilenames = 
    
    /// Get all the XLIFF files in the directory baseName.
    let inDirectory (directory: Path) (project: ProjectName) : XLIFF filename list =
        Directory.EnumerateFiles (string directory, string ^ XLIFF.pattern project)
        |> Seq.map (Path.parse >> Path.name >> Filename)
        |> Seq.toList
    