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


