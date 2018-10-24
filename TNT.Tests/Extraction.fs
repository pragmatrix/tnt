module TNT.Tests.Extraction

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
    let strings = extract ({ Path = AssemblyPath("TNT.Tests.dll"); Language = Language("en-US") })
    strings |> OriginalStrings.strings |> should equal ["duplicate"; "original"]

[<Fact>]
let ``extract from CSharp``() = 
    let strings = extract ({ Path = AssemblyPath("TNT.Tests.CSharp.dll"); Language = Language("en-US") })
    strings |> OriginalStrings.strings |> should equal ["original"]
