module TNT.Library.Library
open System.Runtime.CompilerServices

open TNT.Model

let add (language: LanguageCode) (assembly: AssemblyPath option) =
    ()

let update() = 
    ()

let run (command: Command) = 
    match command with
    | Add(language, assembly) ->
        add language assembly
    | _ -> ()

[<assembly:InternalsVisibleTo("TNT.Tests")>]
do ()