module TNT.Tests.XLIFF

open System.Text
open FunToolbox.FileSystem
open TNT.Model
open TNT.Library
open TNT.Library.ExportModel
open TNT.Library.XLIFF
open FsUnit.Xunit
open Xunit

let record original translated = { 
    Original = original
    Translated = translated
    Contexts = [] 
    Notes = []
}

let translation = { 
    Language = LanguageTag("de-DE")
    Records = [
        record "New" TranslatedString.New
        record "Auto" ^ TranslatedString.NeedsReview "Automatically Translated"
        record "Reviewed" ^ TranslatedString.Final "Reviewed"
        record "Unused" ^ TranslatedString.Unused "Unused"
        {
            Original = "WithContextAndNotes"
            Translated = TranslatedString.NeedsReview "NR"
            Contexts = [ LogicalContext "C1"; LogicalContext "C2" ]
            Notes = [ "Note 1"; "Note 2"]
        }
    ]
}

let linesOf (str: string) =
    str.Split([|'\n'|])
    |> Seq.map (fun str -> str.Trim([|'\r'|]))
    |> Seq.toList

let projectName = ProjectName("project")
let sourceLanguage = LanguageTag("en-US")

[<Fact>]
let ``generates XLIFF``() = 

    let generated = 
        ImportExport.export projectName sourceLanguage translation
        |> List.singleton
        |> generateV12 ExportProfile.MultilingualAppToolkit
        |> string
        |> fun str -> str.Trim()

    let fn = Directory.current() |> Path.extendF (Filename "export01.xlf")
    
    printfn "Generated:\n%s" generated

    let generated = linesOf generated
    generated
    |> should equal (File.loadText Encoding.UTF8 fn |> fun str -> str.Trim() |> linesOf)

[<Fact>]
let ``imports XLIFF``() = 
    let fn = Directory.current() |> Path.extendF (Filename "import01.xlf")
    let xliff = XLIFFV12 ^ File.loadText Encoding.UTF8 fn
    let parsed = parseV12 xliff
    parsed 
    |> should equal [ {
        ProjectName = projectName
        SourceLanguage = sourceLanguage
        TargetLanguage = LanguageTag "de-DE"
        TranslationUnits = [ {
            Source = "New"
            Target = "Neu"
            State = Translated
            Warnings = []
            Contexts = []
            Notes = []
        } ; {
            Source = "Auto";
            Target = "Automatische Uebersetzung";
            State = Final
            Warnings = []
            Contexts = []
            Notes = []
        } ; {
            Source = "Reviewed"
            Target = "Reviewed"
            Warnings = []
            Contexts = []
            State = Final
            Notes = []
        } ; {
            Source = "WithContextAndNotes"
            Target = "NR"
            State = NeedsReview
            Warnings = []
            Contexts = []
            Notes = [
                "Note 1"
                "Note 2"
            ]
        } ]
    } ]
