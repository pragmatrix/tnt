/// Support for retrieving language / culture names from .NET.
module TNT.Library.SystemCultures

open System
open System.Globalization
open TNT.Model

type LanguageInfo = {
    Tag: LanguageTag
    Name: CultureName
    EnglishName: CultureName
}

let All =
    CultureInfo.GetCultures(CultureTypes.AllCultures &&& (~~~CultureTypes.NeutralCultures))
    |> Seq.map ^ fun culture -> {
        Tag = LanguageTag(culture.IetfLanguageTag)
        Name = CultureName(culture.Name)
        EnglishName = CultureName(culture.EnglishName)
    }
    |> Seq.toList

let ByTag = 
    All
    |> Seq.map ^ fun li -> li.Tag, li
    |> Map.ofSeq

/// Returns the name of a language tag in English.
let tryGetName (language: LanguageTag) : CultureName option = 
    ByTag 
    |> Map.tryFind language 
    |> Option.map ^ fun li -> li.EnglishName

let isSupported (language: LanguageTag) : bool = 
    ByTag |> Map.containsKey language

/// Returns a language tag if it maps to a language name or a language tag. 
/// The tag is compared case sensitive.
/// The language name is trimmed and compared in a case insensitive way.
/// First the english names are compared, then the ones of the local culture.
let tryFindTagByTagOrName (name: string) : LanguageTag option =
    let name = Text.trim name
    // we try to locate a tag, this is the fastest, so
    // we go for that first.
    match ByTag.TryFind (LanguageTag name) with
    | Some li -> Some li.Tag
    | None ->
    // then use the english name and compare them
    // with casing.
    let englishMatch = 
        All
        |> Seq.tryFind ^ fun li -> 
            String.Equals(
                string li.EnglishName, name, 
                StringComparison.InvariantCultureIgnoreCase)

    match englishMatch with
    | Some li -> Some li.Tag
    | None ->

    All
    |> Seq.tryFind ^ fun li ->
        String.Equals(
            string li.Name, name, 
            StringComparison.CurrentCultureIgnoreCase)
    |> Option.map ^ fun li -> li.Tag
