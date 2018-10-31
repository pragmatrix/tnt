module TNT.Tests.XLIFF

open System.Text
open FunToolbox.FileSystem
open TNT.Library
open TNT.Library.XLIFF
open TNT.Model
open FsUnit.Xunit
open Xunit

let record original translated = { Original = original; Translated = translated }

let translation = { 
    Language = Language("de-DE")
    Records = [
        record "New" TranslatedString.New
        record "Auto" ^ TranslatedString.NeedsReview "Automatically Translated"
        record "Reviewed" ^ TranslatedString.Final "Reviewed"
        record "Unused" ^ TranslatedString.Unused "Unused"
    ]
}

let linesOf (str: string) =
    str.Split([|'\n'|])
    |> Seq.map (fun str -> str.Trim([|'\r'|]))
    |> Seq.toList

let projectName = ProjectName("project")
let sourceLanguage = Language("en-US")

[<Fact>]
let ``generates XLIFF``() = 
    let generated = 
        ImportExport.export projectName sourceLanguage translation
        |> List.singleton
        |> generateV12
        |> string
        |> fun str -> str.Trim()


    let fn = Directory.current() |> Path.extend "export01.xlf"
    
    let generated = linesOf generated
    generated
    |> should equal (File.loadText Encoding.UTF8 fn |> fun str -> str.Trim() |> linesOf)

[<Fact>]
let ``imports XLIFF``() = 
    let fn = Directory.current() |> Path.extend "import01.xlf"
    let xliff = XLIFFV12 ^ File.loadText Encoding.UTF8 fn
    let parsed = parseV12 xliff
    parsed 
    |> should equal [ {
        Name = string projectName
        SourceLanguage = sourceLanguage
        TargetLanguage = Language "de-DE"
        TranslationUnits = [ {
            Source = "New"
            Target = "Neu"
            State = Translated 
        } ; {
            Source = "Auto";
            Target = "Automatische Uebersetzung";
            State = Final
        } ; {
            Source = "Reviewed"
            Target = "Reviewed"
            State = Final
        } ]
    } ]
