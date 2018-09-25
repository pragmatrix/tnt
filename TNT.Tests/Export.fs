module TNT.Tests.Export

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


[<Fact>]
let ``generates XLIFF``() = 
    let generated = 
        Files.fromTranslations translations
        |> generateV12 (LanguageIdentifier "en-US") 
        |> string
        |> fun str -> str.Trim()


    let fn = Directory.current() |> Path.extend "export01.xlf"
    
    generated
    |> should equal (File.loadText Encoding.UTF8 fn |> fun str -> str.Trim())

    