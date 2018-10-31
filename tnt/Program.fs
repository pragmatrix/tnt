open System
open TNT.Model
open TNT.Library
open TNT.Library.Output
open FunToolbox.FileSystem
open CommandLine

[<Verb("init", HelpText = "Initialize the current directory. Creates the '.tnt' directory and the 'sources.json' file.")>]
type InitOptions = {
    [<Option('l', "language", HelpText = "The source language (code ['-' region]), defaults to 'en-US'.")>]
    Language: string
}

[<Verb("add", HelpText = "Add a new language or assembly.")>]
type AddOptions = {
    // http://www.i18nguy.com/unicode/language-identifiers.html
    // https://www.ietf.org/rfc/rfc3066.txt

    [<Option('l', "language", HelpText = "Language (code ['-' region]) to be added.")>]
    Language: string

    [<Option('a', "assembly", HelpText = "Relative path of the assembly file to be added.")>]
    Assembly: string
}

[<Verb("update", HelpText = "Extract all strings from all assemblies and update the translations.")>]
type UpdateOptions() = 
    class end

[<Verb("gc", HelpText = "Remove all unused translation records.")>]
type GCOptions() = 
    class end

[<Verb("status", HelpText = "Show all the translations and their status.")>]
type StatusOptions() = 
    class end

[<Verb("export", HelpText = "Export all strings from all translation to an XLIFF file.")>]
type ExportOptions = {
    [<Value(0, HelpText = "The directory to export the XLIFF files to. Default is the current directory.")>]
    Directory: string
}

[<Verb("import", HelpText = "Import XLIFF translation files and apply the changes to the translations in the current directory.")>]
type ImportOptions = {
    [<Value(0, HelpText = "The directory to import the XLIFF files from. Default is the current directory.")>]
    Directory: string
}

let private argumentTypes = [|
    typeof<InitOptions>
    typeof<AddOptions>
    typeof<UpdateOptions>
    typeof<GCOptions>
    typeof<StatusOptions>
    typeof<ExportOptions>
    typeof<ImportOptions>
|]

let dispatch (command: obj) = 

    let currentDirectory = Directory.current()

    match command with
    | :? InitOptions as opts ->
        let language = opts.Language |> Option.ofObj |> Option.map Language
        API.init language

    | :? AddOptions as opts -> 
        let language = opts.Language |> Option.ofObj |> Option.map Language
        let assembly = opts.Assembly |> Option.ofObj |> Option.map AssemblyPath

        match language, assembly with
        | None, None
        | Some _, Some _ 
            -> failwith "use either --language or --assembly to specify what should be added"
        | Some language, _ 
            -> API.addLanguage language
        | _, Some assembly 
            -> API.addAssembly assembly
        
    | :? UpdateOptions ->
        API.update()

    | :? GCOptions ->
        API.gc()

    | :? StatusOptions ->
        API.status()

    | :? ExportOptions as opts ->
        let exportDirectory = 
            let relativeDirectory = 
                opts.Directory 
                |> Option.ofObj 
                |> Option.defaultValue "."
            Directory.current() 
            |> Path.extend relativeDirectory

        API.export exportDirectory

    | :? ImportOptions as opts ->
        let importDirectory = 
            let relativeDirectory = 
                opts.Directory 
                |> Option.ofObj 
                |> Option.defaultValue "."
            Directory.current() 
            |> Path.extend relativeDirectory

        API.import importDirectory

    | x -> failwithf "internal error: %A" x

let failed = 5
let ok = 0

let protectedMain args =

    let result = Parser.Default.ParseArguments(args, argumentTypes)

    match result with
    | :? CommandLine.Parsed<obj> as command ->
        dispatch command.Value
        |> Output.run Console.WriteLine
        |> function
        | API.Failed -> failed
        | API.Succeeded -> ok

    | :? CommandLine.NotParsed<obj> ->
        failed
    | x -> failwithf "internal error: %A" x

[<EntryPoint>]
let main args =
    try
        protectedMain args
    with e ->
        printfn "%s" (string e)
        failed
