module TNT.Tests.ImportExport

open TNT.Library.ImportExport
open TNT.Library.XLIFF
open TNT.Model
open FsUnit.Xunit
open Xunit

let inline dump v = 
    printfn "%A" v
    v

let file name lang units = {
    Name = AssemblyFilename name
    TargetLanguage = LanguageIdentifier lang
    TranslationUnits = units 
}

let translation path language records = {
    Assembly = { Language = LanguageIdentifier ""; Path = AssemblyPath path }
    Language = LanguageIdentifier language
    Records = records
}

let tu original translated state = {
    Source = original
    Target = translated
    State = state
}

let record original translated = {
    Original = original
    Translated = translated
}

let key name lang = TranslationId(AssemblyFilename name, LanguageIdentifier lang)

let emptyTranslations : Translation list = []
let emptyWarnings : ImportWarning list = []

[<Fact>]
let ``files with same key are rejected``() = 

    let fileA = file "A.dll" "de" []

    import [] [fileA; fileA]
    |> snd
    |> should equal [
        DuplicateImports (key "A.dll" "de")
    ]

[<Fact>]
let ``files with different keys are processed``() = 

    let fileA = file "A.dll" "de" []
    let fileB = file "A.dll" "us" []

    import [] [fileA; fileB]
    |> snd
    |> should equal [
        TranslationNotFound(key "A.dll" "de")
        TranslationNotFound(key "A.dll" "us") 
    ]

[<Fact>]
let ``original string can not be found``() =

    let translation = translation "A.dll" "us" []

    let tu = tu "source" "target" New
    let file = file "A.dll" "us" [tu]

    import [translation] [file]
    |> snd
    |> should equal [OriginalStringNotFound(key "A.dll" "us", (tu))]

[<Fact>]
let ``updated unused translation (even if set to the same transated string) causes a warning``() = 

    let record = record "source" ^ TranslatedString.Unused "target"
    let translation = translation "A.dll" "de" [record]

    let tu = tu "source" "target" NeedsReview
    let file = file "A.dll" "de" [tu]

    import [translation] [file]
    |> snd
    |> should equal [UnusedTranslationChanged(key "A.dll" "de", (record, record))]

[<Fact>]
let ``new with translation gets ignored``() = 

    let record = record "source" TranslatedString.New
    let translation = translation "A.dll" "de" [record]

    let tu = tu "source" "target" New
    let file = file "A.dll" "de" [tu]

    import [translation] [file]
    |> snd
    |> should equal [IgnoredNewWithTranslation(key "A.dll" "de", (record, tu))]

[<Fact>]
let ``new that resets an entry gets ignored``() = 
    let record = record "source" ^ TranslatedString.NeedsReview "target"
    let translation = translation "A.dll" "de" [record]

    let tu = tu "source" "" New
    let file = file "A.dll" "de" [tu]

    import [translation] [file]
    |> snd
    |> should equal [IgnoredNewReset(key "A.dll" "de", (record, tu))]

[<Fact>]
let ``translation is not returned if nothing changes``() = 
    let translation = 
        translation "A.dll" "de" [record "source" ^ TranslatedString.NeedsReview "target"]
    let file = 
        file "A.dll" "de" [tu "source" "target" NeedsReview]

    import [translation] [file]
    |> should equal (emptyTranslations, emptyWarnings)

[<Fact>]
let ``good case``() = 
    let translationExpected = 
        translation "A.dll" "de" [record "source" ^ TranslatedString.NeedsReview "targetUpdated"]

    let record = record "source" ^ TranslatedString.NeedsReview "target"
    let translation = translation "A.dll" "de" [record]

    let tu = tu "source" "targetUpdated" NeedsReview
    let file = file "A.dll" "de" [tu]

    import [translation] [file]
    |> should equal ([translationExpected], emptyWarnings)

