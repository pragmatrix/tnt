open System
open TNT.Model
open TNT.Library
open TNT.Library.Output
open TNT.Library.ExportModel
open FunToolbox.FileSystem
open CommandLine
open TNT.Library

[<Verb("init", HelpText = "Initialize the current directory. Creates the '.tnt' directory and the 'sources.json' file.")>]
type InitOptions = {
    [<Option('l', "language", HelpText = "The source language (code ['-' region] or name), defaults to 'en-US'.")>]
    Language: string

    [<Value(0)>]
    Unprocessed: string seq
}

[<Verb("add", HelpText = "Add a new language or assembly.")>]
type AddOptions = {
    // http://www.i18nguy.com/unicode/language-identifiers.html
    // https://www.ietf.org/rfc/rfc3066.txt

    [<Option('l', "language", HelpText = "Language (code ['-' region] or name) to be added.")>]
    Language: string

    [<Option('a', "assembly", HelpText = "Relative path of the assembly file to be added.")>]
    Assembly: string

    [<Value(0)>]
    Unprocessed: string seq
}


[<Verb("remove", HelpText = "Remove an assembly from the list of sources.")>]
type RemoveOptions = {
    [<Option('a', "assembly", HelpText = "Relative path, sub-path, or name of the assembly file to be removed.")>]
    Assembly: string

    [<Value(0)>]
    Unprocessed: string seq
}

[<Verb("extract", HelpText = "Extract all strings from all sources and update the translations.")>]
type ExtractOptions = {
    [<Value(0)>]
    Unprocessed: string seq
}

[<Verb("gc", HelpText = "Remove all unused translation records.")>]
type GCOptions = {
    [<Value(0)>]
    Unprocessed: string seq
}

[<Verb("status", HelpText = "Show all the translations and their status.")>]
type StatusOptions = { 
    [<Option('v', "verbose", HelpText = "Show more detailed status information.")>]
    Verbose: bool

    [<Value(0)>]
    Unprocessed: string seq
}

[<Verb("export", HelpText = "Export all strings to XLIFF files.")>]
type ExportOptions = {
    [<Value(0, HelpText = "The language(s) to export, use --all to export all languages.")>]
    Languages: string seq

    [<Option('l', "language", HelpText = "Language (code ['-' region] or name) to be exported.")>]
    Languages2: string seq

    [<Option("all", HelpText = "Export all languages.")>]
    All: bool

    [<Option("to", HelpText = "The directory to export the XLIFF files to. Default is the current directory.")>]
    To: string

    [<Option("format", HelpText = "Export format (default 'excel'). Use 'xliff' for an xliff version 1.2 editor, 'xliff-mat' for the Multilingual App Toolkit.")>]
    For: string
}

[<Verb("import", HelpText = "Import XLIFF translation files and apply the changes to the translations in the current directory.")>]
type ImportOptions = {

    [<Value(0, HelpText = "The files or languages to import, use --all to import all files.")>]
    FilenamesOrLanguages: string seq

    [<Option('l', "language", HelpText = "Language (code ['-' region] or name) to be imported.")>]
    Languages: string seq

    [<Option("from", HelpText = "The directory to import the XLIFF files from. Default is the current directory.")>]
    From: string

    [<Option("all", HelpText = "Import all .xlf or .xliff files that match the current directory's name.")>]
    All: bool
}

[<Verb("translate", HelpText = "Machine translate new strings.")>]
type TranslateOptions = {
    [<Option("all", HelpText = "Translate new strings of all languages.")>]
    All: bool

    [<Value(0, HelpText = "The language(s) to translate new strings to, use --all to translate all languages.")>]
    Languages: string seq
}

[<Verb("sync", HelpText = "Synchronize all the translations in the '.tnt-content' directory.")>]
type SyncOptions = {
    [<Value(0)>]
    Unprocessed: string seq
}

[<Verb("show", HelpText = "Show system related or detail information.")>]
type ShowOptions = {
    [<Value(0, Min = 1, HelpText = 
        "The detail to show: " +
        "'languages' shows the currently supported languages, " +
        "'new' the strings that are not translated yet, " + 
        "'unused' the strings that are not used anymore, " + 
        "'shared' the strings that appear in multiple contexts.")>]
    Details: string seq

    [<Option('l', "language", HelpText = "Language (code ['-' region] or name) that restricts the detail's scope.")>]
    Languages: string seq
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

    let checkUnprocessed unprocessed = 
        if not ^ Seq.isEmpty unprocessed then
            failwithf "found one ore more unprocessed arguments: %s" ^ String.concat "," unprocessed

    match command with
    | :? InitOptions as opts ->
        opts.Unprocessed |> checkUnprocessed
        let language = opts.Language |> Option.ofObj |> Option.map resolveLanguage
        API.init language

    | :? AddOptions as opts -> 
        opts.Unprocessed |> checkUnprocessed
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
        opts.Unprocessed |> checkUnprocessed
        let assembly = opts.Assembly |> Option.ofObj |> Option.map RPath.parse
        match assembly with
        | None 
            -> failwith "use --assembly to specify which assembly should be removed"
        | Some assembly 
            -> API.removeAssembly assembly
        
    | :? ExtractOptions as opts ->
        opts.Unprocessed |> checkUnprocessed
        API.extract()

    | :? GCOptions as opts ->
        opts.Unprocessed |> checkUnprocessed
        API.gc()

    | :? StatusOptions as opts ->
        opts.Unprocessed |> checkUnprocessed
        API.status opts.Verbose

    | :? ExportOptions as opts ->
        let selector = 
            if opts.All then SelectAll else
            [ opts.Languages; opts.Languages2 ]
            |> Seq.concat
            |> Seq.map resolveLanguage 
            |> Seq.toList
            |> Select

        if selector = Select [] then
            failwith "No languages selected, enter them as arguments or use --all to select all languages."

        let exportPath = 
            opts.To 
            |> Option.ofObj 
            |> Option.defaultValue "."
            |> ARPath.parse

        let exportProfile = 
            opts.For
            |> Option.ofObj
            |> Option.map ExportFormat.parse
            |> Option.defaultValue ExportFormat.Excel

        API.export selector exportPath exportProfile

    | :? ImportOptions as opts ->

        let importDirectory = 
            let relativeDirectory = 
                opts.From 
                |> Option.ofObj 
                |> Option.defaultValue "."
                |> RPath.parse
            Directory.current() 
            |> Path.extend relativeDirectory

        let project = API.projectName()

        let AllExporters = [ 
            XLIFF.exporter XLIFF12
            XLIFF.exporter XLIFF12MultilingualAppToolkit
            Excel.Exporter
        ]

        let files =
            if opts.All then
                AllExporters
                |> Seq.collect ^ fun exporter -> 
                    exporter.FilesInDirectory project importDirectory
                    |> Seq.map ^ fun fn -> exporter, fn
                |> Seq.distinctBy ^ snd
                |> Seq.map ^ fun (exporter, fn) -> 
                    exporter, importDirectory |> Path.extendF fn
                |> Seq.toList
            else

            let resolveLanguage (language: string) =
                    // when a language tag or name is used, 
                    // only the files with the default extension are imported. Other files
                    // must be explicitly provided by filename.
                    let language = resolveLanguage language
                    let potentialFilenames = 
                        Exporters.allDefaultFilenames project language AllExporters
                    let potentialFiles = 
                        potentialFilenames
                        |> List.mapSnd ^ Filename.at importDirectory
                        |> List.filter (File.exists << snd)

                    match potentialFiles with
                    | [] -> 
                        failwithf "Found no file for language %s, expected one of '%s'." 
                            language.Formatted 
                            (potentialFilenames 
                            |> Seq.map (string << snd)
                            |> Seq.distinct
                            |> String.concat ",")
                    | [one] -> 
                        one
                    | _ -> 
                        failwithf "For more than one file for language %s found." language.Formatted

            let resolveFileOrLanguage (filePathOrLanguage: string) = 
                // we check first by filename extension.
                let resolvedViaFilename = 
                    filePathOrLanguage 
                    |> ARPath.tryParse 
                    |> Option.bind ^ fun path -> 
                        Exporters.tryResolveExporterFromFilename (Filename.ofARPath path) AllExporters
                        |> Option.map ^ fun exporter -> exporter, path

                match resolvedViaFilename with
                | Some (exporter, path) 
                    -> exporter, path |> ARPath.at importDirectory
                | None 
                    -> resolveLanguage filePathOrLanguage

            match Seq.toList opts.FilenamesOrLanguages, Seq.toList opts.Languages with
            | [], [] -> failwith "No languages selected, enter them as arguments or '-l', or use --all to select all languages"
            | filenamesOrLanguages, languages ->
                
                let viaArguments = 
                    filenamesOrLanguages
                    |> List.map resolveFileOrLanguage
                let viaOptions = 
                    languages
                    |> List.map resolveLanguage

                [viaArguments; viaOptions]
                |> Seq.collect id
                |> Seq.distinctBy snd
                |> Seq.toList

        API.import files

    | :? TranslateOptions as opts ->

        let selector = 
            if opts.All then SelectAll else
            opts.Languages 
            |> Seq.map resolveLanguage 
            |> Seq.toList
            |> Select

        if selector = Select [] then
            failwith "No languages selected, use -l or --all to select one or more languages."

        API.translate selector

    | :? SyncOptions as opts ->
        opts.Unprocessed |> checkUnprocessed
        API.sync()

    | :? ShowOptions as opts ->

        let selector = 
            let languages =
                opts.Languages
                |> Seq.toList
            if languages = [] then SelectAll else
            languages 
            |> Seq.map resolveLanguage 
            |> Seq.toList
            |> Select

        API.show selector (opts.Details |> Seq.toList)

    | x -> failwithf "internal error: %A" x

let failed = 5
let ok = 0

let protectedMain args =

    match Parser.Default.ParseArguments(args, argumentTypes) with
    | :? CommandLine.Parsed<obj> as command ->
        dispatch command.Value
        |> Output.run Console.WriteLine
        |> function
            | Ok() -> ok
            | Error() -> failed

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
