module TNT.Tests.Extraction

open FunToolbox.FileSystem
open FsUnit.Xunit.Typed
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
    let strings, warnings = extract path
    printfn "Warnings:\n%A" warnings
    strings 
    |> OriginalStrings.strings 
    |> List.map fst 
    |> should equal ["duplicate"; "original"]

[<Fact>]
let ``extract from CSharp``() = 
    let path = Directory.current() |> Path.extend "TNT.Tests.CSharp.dll"
    let strings, warnings = extract path
    printfn "Warnings:\n%A" warnings
    strings 
    |> OriginalStrings.strings 
    |> should equal [
        "Formattable {0}", [ LogicalContext "TNT.Tests.CSharp.FormattableString" ]
        "explicit", [ LogicalContext "TNT.Tests.CSharp.Explicit" ]
        "explicit2", [ LogicalContext "TNT.Tests.CSharp.Explicit" ]
        "original", [ LogicalContext "TNT.Tests.CSharp.TranslateableTextClass" ]
        "originalCG", [ LogicalContext "TNT.Tests.CSharp" ]
    ]

[<Fact>]
let ``extraction may cause errors and retrieves a physical context``() = 
    let path = Directory.current() |> Path.extend "TNT.Tests.CSharp.dll"
    let _, errors = extract path
    errors |> printfn "%A"
    match errors with
    | [] -> failwith "failed, no error"
    | [_, context] ->
        context.Physical.IsSome |> should equal true
    | _ -> failwith "too many errors, expect one"

