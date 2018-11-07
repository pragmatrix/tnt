module TNT.Tests.ImportExport

open TNT.Library
open TNT.Library.XLIFF
open TNT.Library.ImportExport
open TNT.Model
open FsUnit.Xunit
open Xunit

let inline dump v = 
    printfn "%A" v
    v

let project = ProjectName("project")

let file lang units = {
    Name = string project
    SourceLanguage = LanguageTag "en-US"
    TargetLanguage = LanguageTag lang
    TranslationUnits = units 
}

let translation language records = {
    Language = LanguageTag language
    Records = records
}

let tu original translated state = {
    Source = original
    Target = translated
    State = state
    Notes = []
}

let record original translated = {
    Original = original
    Translated = translated
    Contexts = []
    Notes = []
}

let emptyTranslations : Translation list = []
let emptyWarnings : ImportWarning list = []

let import = import project
let lang l = LanguageTag(l)

[<Fact>]
let ``wrong project``() = 
    let fileA = file "de" []
    let expectedProject = ProjectName("expected")
    ImportExport.import expectedProject [] [fileA]
    |> snd
    |> should equal [
        ProjectMismatch(project, expectedProject)
    ]

[<Fact>]
let ``files with same key are rejected``() = 

    let fileA = file "de" []

    import [] [fileA; fileA]
    |> snd
    |> should equal [
        DuplicateImports (lang "de")
    ]

[<Fact>]
let ``files with different keys are processed``() = 

    let fileA = file "de" []
    let fileB = file "us" []

    import [] [fileA; fileB]
    |> snd
    |> should equal [
        TranslationNotFound(lang "de")
        TranslationNotFound(lang "us") 
    ]

[<Fact>]
let ``original string can not be found``() =

    let translation = translation "us" []

    let tu = tu "source" "target" New
    let file = file "us" [tu]

    import [translation] [file]
    |> snd
    |> should equal [OriginalStringNotFound(lang "us", tu)]

[<Fact>]
let ``updated unused translation (even if set to the same transated string) causes a warning``() = 

    let record = record "source" (TranslatedString.Unused "target")
    let translation = translation "de" [record]

    let tu = tu "source" "target" NeedsReview
    let file = file "de" [tu]

    import [translation] [file]
    |> snd
    |> should equal [UnusedTranslationChanged(lang "de", (record, record))]

[<Fact>]
let ``new with translation gets ignored``() = 

    let record = record "source" TranslatedString.New
    let translation = translation "de" [record]

    let tu = tu "source" "target" New
    let file = file "de" [tu]

    import [translation] [file]
    |> snd
    |> should equal [IgnoredNewWithTranslation(lang "de", (record, tu))]

[<Fact>]
let ``new that resets an entry gets ignored``() = 
    let record = record "source" (TranslatedString.NeedsReview "target")
    let translation = translation "de" [record]

    let tu = tu "source" "" New
    let file = file "de" [tu]

    import [translation] [file]
    |> snd
    |> should equal [IgnoredNewReset(lang "de", (record, tu))]

[<Fact>]
let ``translation is not returned if nothing changes``() = 
    let translation = 
        translation "de" [record "source" (TranslatedString.NeedsReview "target")]
    let file = 
        file "de" [tu "source" "target" NeedsReview]

    import [translation] [file]
    |> should equal (emptyTranslations, emptyWarnings)

[<Fact>]
let ``good case``() = 
    let translationExpected = 
        translation "de" [record "source" (TranslatedString.NeedsReview "targetUpdated")]

    let translation = translation "de" [record "source" ^ TranslatedString.NeedsReview "target"]

    let file = file "de" [tu "source" "targetUpdated" NeedsReview]

    import [translation] [file]
    |> should equal ([translationExpected], emptyWarnings)

[<Fact>]
let ``context get ignored and notes get overwritten by import``() = 

    let translationExpected = translation "de" [ { 
        Original = "source"
        Translated = TranslatedString.NeedsReview "target"
        Contexts = [ LogicalContext "LC1"; LogicalContext "LC2" ]
        Notes = [ "Note 1"; "Note 2"]
    } ]

    let currentTranslation = translation "de" [ {
        Original = "source"
        Translated = TranslatedString.NeedsReview "target"
        Contexts = [ LogicalContext "LC1"; LogicalContext "LC2" ]
        Notes = [ "Note 3"; "Note 4"]
    } ]

    let importFile = file "de" [{ 
        Source = "source"
        Target = "target"
        State = NeedsReview 
        Notes = [ "Context:LC2"; "   Context: LC3 "; "Note 1"; "Note 2" ]
    } ]
    
    import [currentTranslation] [importFile]
    |> should equal ([translationExpected], emptyWarnings)



    

