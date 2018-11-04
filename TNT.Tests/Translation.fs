module TNT.Tests.Translation

open TNT.Model
open TNT.Library
open FsUnit.Xunit
open Xunit

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

        