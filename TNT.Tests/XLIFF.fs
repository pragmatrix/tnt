module TNT.Tests.XLIFF

open FsUnit.Xunit
open Xunit
open TNT.Library.XLIFF
open TNT.Model
open FunToolbox.FileSystem
open System.Text

let record original translated state = TranslationRecord(OriginalString(original), TranslatedString(state, translated))

let translations = [
    Translation (TranslationId(AssemblyPath("/tmp/test.dll"), LanguageIdentifier("de-DE")), [
        record "New" "" TranslatedStringState.New
        record "Auto" "Automatically Translated" TranslatedStringState.Auto
        record "Reviewed" "Reviewed" TranslatedStringState.Reviewed
        record "Unused" "Unused" TranslatedStringState.Unused
    ])
]

let linesOf (str: string) =
    str.Split([|'\n'|])
    |> Seq.map (fun str -> str.Trim([|'\r'|]))
    |> Seq.toList

[<Fact>]
let ``generates XLIFF``() = 
    let generated = 
        Files.fromTranslations translations
        |> generateV12 (LanguageIdentifier "en-US") 
        |> string
        |> fun str -> str.Trim()


    let fn = Directory.current() |> Path.extend "export01.xlf"
    
    linesOf generated
    |> should equal (File.loadText Encoding.UTF8 fn |> fun str -> str.Trim() |> linesOf)

[<Fact>]
let ``imports XLIFF``() = 
    let fn = Directory.current() |> Path.extend "import01.xlf"
    let xliff = XLIFFV12 ^ File.loadText Encoding.UTF8 fn
    let parsed = parseV12 xliff
    parsed 
    |> should equal [ {
        Name = AssemblyFilename "test.dll"
        TargetLanguage = LanguageIdentifier "de-DE"
        TranslationUnits = [ {
            Source = "New"
            Target = "Neu"
            TargetState = Translated 
        } ; {
            Source = "Auto";
            Target = "Automatische Uebersetzung";
            TargetState = Final
        } ; {
            Source = "Reviewed"
            Target = "Reviewed"
            TargetState = Final
        } ]
    } ]
