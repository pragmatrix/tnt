/// Types to communicate with import and export methods.
module TNT.Library.ExportModel

open TNT.Model
open FunToolbox.FileSystem

/// The state of a translation. 
type TargetState = 
    | New
    | NeedsReview
    | Translated
    | Final

type TranslationUnit = {
    Source: string
    Target: string
    State: TargetState
    Warnings: string list
    Contexts: string list
    Notes: string list
}

type File = {
    ProjectName: ProjectName
    SourceLanguage: LanguageTag
    TargetLanguage: LanguageTag
    TranslationUnits: TranslationUnit list
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
        | "xls" -> Excel
        | "xliff" 
        | "xlf" -> XLIFF ^ XLIFF12
        | "xliff-mat"
        | "xlf-mat"
        | "mat" -> XLIFF ^ XLIFF12MultilingualAppToolkit
        | unexpected -> failwithf "unexpected export format: '%s'" unexpected

type Exporter<'format> = {
    Extensions: string list
    DefaultExtension: string
    FilenameForLanguage: ProjectName -> LanguageTag -> 'format filename
    ExportToPath: Path -> File -> unit
}