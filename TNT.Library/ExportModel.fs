/// Types to communicate with import and export methods.
module TNT.Library.ExportModel

open TNT.Model
open FunToolbox.FileSystem

/// Generic tag for Export related Filenames
type Export = Export

/// The state of a translation. 
type TargetState = 
    | New
    | NeedsReview
    | Translated
    | Final

type ExportUnit = {
    Source: string
    Target: string
    State: TargetState
    Warnings: string list
    Contexts: string list
    Notes: string list
}

type ImportUnit = {
    Source: string
    Target: string
    State: TargetState
    Notes: string list option
}

type File<'unit> = {
    ProjectName: ProjectName
    SourceLanguage: LanguageTag
    TargetLanguage: LanguageTag
    TranslationUnits: 'unit list
}

type XLIFFFormat = 
    | XLIFF12
    | XLIFF12MultilingualAppToolkit
    member this.RequiresGroups = 
        match this with
        | XLIFF12 -> false
        | XLIFF12MultilingualAppToolkit -> true

type ExportFormat =
    | Excel
    | XLIFF of XLIFFFormat

module ExportFormat =

    let parse = function
        | "excel" 
        | "xlsx"
        | "xls" -> Excel
        | "xliff" 
        | "xlf" -> XLIFF ^ XLIFF12
        | "xliff-mat"
        | "xlf-mat"
        | "mat" -> XLIFF ^ XLIFF12MultilingualAppToolkit
        | unexpected -> failwithf "unexpected export format: '%s'" unexpected

type Exporter = {
    Extensions: string list
    DefaultExtension: string
    DefaultFilename: ProjectName -> LanguageTag -> Export filename
    SaveToPath: Path -> File<ExportUnit> -> unit
    LoadFromPath: Path -> File<ImportUnit> list
    FilesInDirectory: ProjectName -> Path -> Export filename list
}

module Exporters = 
    
    let tryfindExporterByFilename 
        (Filename filename) 
        (exporters: Exporter list) : Exporter option = 
        exporters
        |> Seq.tryFind ^ fun exporter ->
            exporter.Extensions 
            |> Seq.exists ^ fun ext -> filename.endsWith ext
        
    let allDefaultFilenames 
        (project: ProjectName) 
        (language: LanguageTag) 
        (exporters: Exporter list) 
        : (Exporter * Export filename) list =
        exporters 
        |> List.map ^ fun exporter -> 
            exporter, exporter.DefaultFilename project language
