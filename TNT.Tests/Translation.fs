module TNT.Tests.Translation

open System.Text
open TNT.Model
open TNT.Library
open FsUnit.Xunit
open Xunit
open FunToolbox.FileSystem

module Counters =

    [<Fact>]
    let ``counting zero translation records``() = 
        {
            Language = LanguageTag("")
            Records = []
        }
        |> TranslationCounters.ofTranslation
        |> should equal TranslationCounters.zero

    [<Theory>]
    [<InlineData("translation-xx.json", "xx")>]
    [<InlineData("translation-xx-yy.json", "xx-yy")>]
    [<InlineData("translation-xx-yyyjson", null)>]
    [<InlineData("translation-.json", null)>]
    [<InlineData("translation-..json", ".")>]
    [<InlineData("-translation-..json", null)>]
    [<InlineData("translation-..json-", null)>]
    let ``language can be extracted from filename``(fn: string, result: string) =
        let extracted = 
            match Translation.languageOf (Filename fn) with
            | None -> null
            | Some tag -> string tag
        extracted |> should equal result

    [<Fact>]
    let ``translation deserializes correctly``() =
        let path = Directory.current() |> Path.extend "translation.json"
        File.loadText Encoding.UTF8 path
        |> Translation.deserialize
        |> should equal {
            Language = LanguageTag "de"
            Records = [{
                Original = "NewOriginal"
                Translated = TranslatedString.New
                Contexts = []
            };
            {
                Original = "NROriginal"
                Translated = TranslatedString.NeedsReview "NRTranslated"
                Contexts = []
            };
            {
                Original = "FOriginal"
                Translated = TranslatedString.Final "FTranslated"
                Contexts = []
            };
            {
                Original = "UOriginal"
                Translated = TranslatedString.Unused "UTranslated"
                Contexts = []
            };
            { 
                Original = "NROriginalEmptyContext"
                Translated = TranslatedString.NeedsReview "NRTranslated"
                Contexts = []
            };
            { 
                Original = "NROriginalWithContext"
                Translated = TranslatedString.NeedsReview "NRTranslated"
                Contexts = [LogicalContext "Context"]
            } ]
        }
