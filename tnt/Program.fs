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
type StatusOptions = { 
    [<Option('v', "verbose", HelpText = "Provide more detailed status information.")>]
    Verbose: bool
}

[<Verb("export", HelpText = "Export all strings from all translations to an XLIFF file.")>]
type ExportOptions = {
    [<Value(0, HelpText = "The directory to export the XLIFF files to. Default is the current directory.")>]
    Directory: string
}

[<Verb("import", HelpText = "Import XLIFF translation files and apply the changes to the translations in the current directory.")>]
type ImportOptions = {
    [<Value(0, HelpText = "The directory to import the XLIFF files from. Default is the current directory.")>]
    Directory: string
}

[<Verb("translate", HelpText = "Machine translate new strings")>]
type TranslateOptions = {
    // [<Option('p', "provider", HelpText = "The machine translation provider, default is 'Google'.")>]
    // Provider: string

    // [<Option("list", HelpText = "List the target languages supported by the machine translation provider.")>]
    // List: bool

    [<Value(0, HelpText = "The language(s) to translate to. If none are provided, all new strings of all languages are translated.")>]
    Languages: string seq
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

    | :? StatusOptions as opts ->
        API.status opts.Verbose

    | :? ExportOptions as opts ->
        let exportPath = 
            opts.Directory 
            |> Option.ofObj 
            |> Option.defaultValue "."
            |> ARPath.ofString

        API.export exportPath

    | :? ImportOptions as opts ->
        let importDirectory = 
            let relativeDirectory = 
                opts.Directory 
                |> Option.ofObj 
                |> Option.defaultValue "."
            Directory.current() 
            |> Path.extend relativeDirectory

        API.import importDirectory

    | :? TranslateOptions as opts ->
        let languages = 
            opts.Languages 
            |> Seq.map Language
            |> Seq.toList

        API.translate languages

    | x -> failwithf "internal error: %A" x

let failed = 5
let ok = 0

let protectedMain args =

    match Parser.Default.ParseArguments(args, argumentTypes) with
    | :? CommandLine.Parsed<obj> as command ->
        dispatch command.Value
        |> Output.run Console.WriteLine
        |> function
        | API.Failed -> failed
        | API.Succeeded -> ok

    | :? CommandLine.NotParsed<obj> ->
        failed

    | x -> 
        failwithf "internal error: %A" x

[<EntryPoint>]
let main args =
    try
        protectedMain args
    with e ->
        printfn "%s" (string e.Message)
        failed
