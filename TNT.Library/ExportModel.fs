/// Types to communicate with import and export methods.
module TNT.Library.ExportModel

open TNT.Model

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
