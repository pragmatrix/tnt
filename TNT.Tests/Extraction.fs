module TNT.Tests.Extraction

open FsUnit.Xunit
open Xunit

open TNT.Model
open TNT.Library.StringExtractor

module X = 
    let o = "original".t
    let a = "duplicate".t
    let b = "duplicate".t

[<Fact>]
let ``extract from FSharp``() = 
    let strings = extract (AssemblyPath("TNT.Tests.dll"))
    strings |> List.map string |> should equal ["duplicate"; "original"]

[<Fact>]
let ``extract from CSharp``() = 
    let strings = extract (AssemblyPath("TNT.Tests.CSharp.dll"))
    strings |> List.map string |> should equal ["original"]
