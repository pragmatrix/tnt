/// Types to communicate with import and export methods.
module TNT.Library.ExportModel

open TNT.Model

/// The state of a translation. 
/// Note: Multilingual app toolkit supports 
/// "new", "need-review-translation", "translated", and "final".
type TargetState = 
    | New
    | NeedsReview
    | Translated
    | Final
    override this.ToString() = 
        match this with
        | New -> "new"
        | NeedsReview -> "needs-review-translation"
        | Translated -> "translated"
        | Final -> "final"

module TargetState = 
    
    let tryParse = function
        | "new" -> Ok New
        | "needs-review-translation" -> Ok NeedsReview
        | "translated" -> Ok Translated
        | "final" -> Ok Final
        | str  -> Error str

type TranslationUnit = {
    Source: string
    Target: string
    State: TargetState
    Notes: string list
}

type File = {
    ProjectName: ProjectName
    SourceLanguage: LanguageTag
    TargetLanguage: LanguageTag
    TranslationUnits: TranslationUnit list
}
