open System
open CommandLine
open TNT.Model
open TNT.Library
open TNT.Library.Output

[<Verb("add", HelpText = "Add a new language to all existing translations or to one specific assembly.")>]
type AddOptions = {

    // http://www.i18nguy.com/unicode/language-identifiers.html
    // https://www.ietf.org/rfc/rfc3066.txt
    [<Option('l', "lang", Required = true, HelpText = "Language identifier (code ['-' region]).")>]
    Language: string

    [<Value(0, HelpText = "Relative path of the assembly file.")>]
    Assembly: string option
}

[<Verb("update", HelpText = "Extract all strings from all assemblies or one specific assembly and update the translation files.")>]
type UpdateOptions = {

    [<Value(0, HelpText = "Relative path of the asseembly file.")>]
    Assembly: string option
}

(*

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
*)

[<EntryPoint>]
let main args =

    let failed = 5
    let ok = 0

    try
        let result = 
            args 
            |> Parser.Default.ParseArguments<AddOptions, UpdateOptions>

        match result with
        | :? CommandLine.Parsed<obj> as command ->

            let output =
                match command.Value with
                | :? AddOptions as opts -> 
                    API.add 
                        (LanguageIdentifier(opts.Language)) 
                        (opts.Assembly |> Option.map AssemblyPath)
                | :? UpdateOptions as opts ->
                    API.update
                        (opts.Assembly |> Option.map AssemblyPath)
                | x -> failwithf "internal error: %A" x

            let result = Output.run Console.WriteLine output
            match result with
            | API.Failed -> failed
            | API.Succeeded -> ok

        | :? CommandLine.NotParsed<obj> as np 
            -> failwithf "command line error: %A" (np.Errors |> Seq.toList)
        | x -> failwithf "internal error: %A" x

    with e ->
        printfn "%s" e.Message
        failed
