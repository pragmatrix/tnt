module TNT.Tests.Translation

open System.Text
open TNT.Model
open TNT.Library
open FsUnit.Xunit.Typed
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
                Notes = []
            };
            {
                Original = "NROriginal"
                Translated = TranslatedString.NeedsReview "NRTranslated"
                Contexts = []
                Notes = []
            };
            {
                Original = "FOriginal"
                Translated = TranslatedString.Final "FTranslated"
                Contexts = []
                Notes = []
            };
            {
                Original = "UOriginal"
                Translated = TranslatedString.Unused "UTranslated"
                Contexts = []
                Notes = []
            };
            { 
                Original = "NROriginalEmptyContext"
                Translated = TranslatedString.NeedsReview "NRTranslated"
                Contexts = []
                Notes = []
            };
            { 
                Original = "NROriginalWithContext"
                Translated = TranslatedString.NeedsReview "NRTranslated"
                Contexts = [LogicalContext "Context"]
                Notes = []
            };
            { 
                Original = "NROriginalWithNotes"
                Translated = TranslatedString.NeedsReview "NRTranslated"
                Contexts = []
                Notes = [ "Note 1"; "Note 2" ]
            } ]
        }
