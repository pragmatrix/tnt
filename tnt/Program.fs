open System
open TNT.Model
open TNT.Library
open TNT.Library.Output
open FunToolbox.FileSystem
open CommandLine

[<Verb("init", HelpText = "Initialize the current directory. Creates the '.tnt' directory and the 'sources.json' file.")>]
type InitOptions = {
    [<Option('l', "language", HelpText = "The source language (code ['-' region] or name), defaults to 'en-US'.")>]
    Language: string
}

[<Verb("add", HelpText = "Add a new language or assembly.")>]
type AddOptions = {
    // http://www.i18nguy.com/unicode/language-identifiers.html
    // https://www.ietf.org/rfc/rfc3066.txt

    [<Option('l', "language", HelpText = "Language (code ['-' region] or name) to be added.")>]
    Language: string

    [<Option('a', "assembly", HelpText = "Relative path of the assembly file to be added.")>]
    Assembly: string
}

[<Verb("remove", HelpText = "Remove an assembly from the list of sources.")>]
type RemoveOptions = {
    [<Option('a', "assembly", HelpText = "Relative path, sub-path, or name of the assembly file to be removed.")>]
    Assembly: string
}

[<Verb("extract", HelpText = "Extract all strings from all sources and update the translations.")>]
type ExtractOptions() = 
    class end

[<Verb("gc", HelpText = "Remove all unused translation records.")>]
type GCOptions() = 
    class end

[<Verb("status", HelpText = "Show all the translations and their status.")>]
type StatusOptions = { 
    [<Option('v', "verbose", HelpText = "Provide more detailed status information.")>]
    Verbose: bool
}

[<Verb("export", HelpText = "Export all strings to XLIFF files.")>]
type ExportOptions = {
    [<Value(0, HelpText = "The language(s) to export, use --all to export all languages.")>]
    Languages: string seq

    [<Option("all", HelpText = "Export all languages.")>]
    All: bool

    [<Option("to", HelpText = "The directory to export the XLIFF files to. Default is the current directory.")>]
    To: string
}

[<Verb("import", HelpText = "Import XLIFF translation files and apply the changes to the translations in the current directory.")>]
type ImportOptions = {

    [<Value(0, HelpText = "The directory to import the XLIFF files from. Default is the current directory.")>]
    From: string

    [<Value(0, HelpText = "The files or languages to import, use --all to import all files.")>]
    FilesOrLanguages: string seq

    [<Option("all", HelpText = "Import all .xlf or .xliff files that match the current directory's name.")>]
    All: bool
}

[<Verb("translate", HelpText = "Machine translate new strings.")>]
type TranslateOptions = {
    [<Value(0, HelpText = "The language(s) to translate to. If none are provided, all new strings of all languages are translated.")>]
    Languages: string seq
}

[<Verb("sync", HelpText = "Synchronize all the translations in the '.tnt-content' directory.")>]
type SyncOptions() = 
    class end

[<Verb("show", HelpText = "Show system related information.")>]
type ShowOptions = {
    [<Value(0, HelpText = "The information to show, 'languages' shows the currently supported languages.")>]
    Categories: string seq
}

let private argumentTypes = [|
    typeof<InitOptions>
    typeof<AddOptions>
    typeof<RemoveOptions>
    typeof<ExtractOptions>
    typeof<GCOptions>
    typeof<StatusOptions>
    typeof<ExportOptions>
    typeof<ImportOptions>
    typeof<TranslateOptions>
    typeof<SyncOptions>
    typeof<ShowOptions>
|]

let private resolveLanguage (nameOrTag: string) : LanguageTag = 
    SystemCultures.tryFindTagByTagOrName nameOrTag
    |> Option.defaultWith ^ fun () -> LanguageTag(Text.trim nameOrTag)

let dispatch (command: obj) = 

    match command with
    | :? InitOptions as opts ->
        let language = opts.Language |> Option.ofObj |> Option.map resolveLanguage
        API.init language

    | :? AddOptions as opts -> 
        let language = opts.Language |> Option.ofObj |> Option.map resolveLanguage
        let assembly = opts.Assembly |> Option.ofObj |> Option.map RPath.parse

        match language, assembly with
        | None, None
        | Some _, Some _ 
            -> failwith "use either --language or --assembly to specify what should be added"
        | Some language, _ 
            -> API.addLanguage language
        | _, Some assembly 
            -> API.addAssembly assembly

    | :? RemoveOptions as opts ->
        let assembly = opts.Assembly |> Option.ofObj |> Option.map RPath.parse
        match assembly with
        | None 
            -> failwith "use --assembly to specify which assembly should be removed"
        | Some assembly 
            -> API.removeAssembly assembly
        
    | :? ExtractOptions ->
        API.extract()

    | :? GCOptions ->
        API.gc()

    | :? StatusOptions as opts ->
        API.status opts.Verbose

    | :? ExportOptions as opts ->
        let selector = 
            if opts.All then SelectAll else
            opts.Languages 
            |> Seq.map resolveLanguage 
            |> Seq.toList
            |> Select

        let exportPath = 
            opts.To 
            |> Option.ofObj 
            |> Option.defaultValue "."
            |> ARPath.parse

        API.export selector exportPath 

    | :? ImportOptions as opts ->

        let importDirectory = 
            let relativeDirectory = 
                opts.From 
                |> Option.ofObj 
                |> Option.defaultValue "."
            Directory.current() 
            |> Path.extend relativeDirectory

        let project = API.projectName()

        let files =
            if opts.All then
                XLIFFFilenames.inDirectory importDirectory project
                |> List.map ^ fun fn -> importDirectory |> Path.extendF fn
            else
            let resolveFile (filePathOrLanguage: string) = 
                match XLIFF.properPath filePathOrLanguage with
                | Some path -> path |> ARPath.at importDirectory
                | None -> 
                    // when a language tag or name is used, 
                    // only .xlf files are imported and .xliff files are being ignored
                    // and must be explicitly passed as a path.
                    resolveLanguage filePathOrLanguage
                    |> XLIFF.defaultFilenameForLanguage project
                    |> Filename.at importDirectory

            opts.FilesOrLanguages
            |> Seq.map resolveFile
            |> Seq.toList

        API.import files

    | :? TranslateOptions as opts ->
        let languages = 
            opts.Languages 
            |> Seq.map resolveLanguage
            |> Seq.toList

        API.translate languages

    | :? SyncOptions ->

        API.sync()

    | :? ShowOptions as opts ->

        API.show (opts.Categories |> Seq.toList)

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
