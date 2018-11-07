module TNT.Tests.Extraction

open FunToolbox.FileSystem
open FsUnit.Xunit
open Xunit
open TNT.Model
open TNT.Library.StringExtractor

module X = 
    let o = "original".t
    let a = "duplicate".t
    let b = "duplicate".t

/// This implicitly tests if empty function bodies are supported.
type Interface = 
    abstract EmptyFunction : unit -> unit

[<Fact>]
let ``extract from FSharp``() =
    let path = Directory.current() |> Path.extend "TNT.Tests.dll"
    let strings = extract path
    strings 
    |> OriginalStrings.strings 
    |> List.map fst 
    |> should equal ["duplicate"; "original"]

[<Fact>]
let ``extract from CSharp``() = 
    let path = Directory.current() |> Path.extend "TNT.Tests.CSharp.dll"
    let strings = extract path
    strings 
    |> OriginalStrings.strings 
    |> should equal 
        [
            "original", [ LogicalContext "TNT.Tests.CSharp.TranslateableTextClass" ]
            "originalCG", [ LogicalContext "TNT.Tests.CSharp" ]
        ]
