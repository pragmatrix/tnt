[<AutoOpen>]
module TNT.Library.Semantic

open TNT.Model

module TranslatedStringState = 

    let fromString str = 
        match str with
        | "new" -> TranslatedStringState.New
        | "auto" -> TranslatedStringState.Auto
        | "verified" -> TranslatedStringState.Verified
        | "unused" -> TranslatedStringState.Unused
        | str -> failwithf "'%s': invalid translated string state" str

module TranslationRecord = 

    let createNew original = 
        TranslationRecord(original, TranslatedString(TranslatedStringState.New, ""))

    