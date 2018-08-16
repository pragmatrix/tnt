open Argu

type AddArgs =
    | [<AltCommandLine("-l"); Mandatory>] Language of langauge: string
    | [<AltCommandLine("-a")>] Assembly of assembly: string
    with
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | Language _ -> "The language code"
                | Assembly _ -> "The relative path to the assembly that's used for extracting the original strings"

type TNTArgs = 
    | [<CliPrefix(CliPrefix.None)>] Add of ParseResults<AddArgs>
    | [<CliPrefix(CliPrefix.None)>] Update
    with
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Add _ -> "Adds a new language"
                | Update -> "Extracts all strings from the assemblies and updates the translations files."

[<EntryPoint>]
let main argv =

    let failed = 5
    let ok = 0

    try
        let parser = ArgumentParser.Create<TNTArgs>(programName = "tnt.exe")
        parser.ParseCommandLine(inputs = argv, raiseOnUsage = true) |> ignore
        ok
    with e ->
        printfn "%s" e.Message
        failed
