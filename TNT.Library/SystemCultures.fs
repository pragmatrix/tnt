/// Support for retrieving language / culture names from .NET.
module TNT.Library.SystemCultures

open TNT.Model
open System.Globalization

let All = 
    CultureInfo.GetCultures(CultureTypes.AllCultures &&& (~~~CultureTypes.NeutralCultures))
    |> Seq.map ^ fun culture -> 
        LanguageTag(culture.IetfLanguageTag), CultureName(culture.EnglishName)
    |> Map.ofSeq

let tryGetName (language: LanguageTag) : CultureName option = 
    All |> Map.tryFind language

let isSupported (language: LanguageTag) : bool = 
    All |> Map.containsKey language