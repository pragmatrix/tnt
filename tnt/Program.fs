﻿open System
open TNT.Model
open TNT.Library
open TNT.Library.Output
open FunToolbox.FileSystem
open CommandLine

[<Verb("add", HelpText = "Add a new language to all existing translations or to one specific assembly.")>]
type AddOptions = {
    // http://www.i18nguy.com/unicode/language-identifiers.html
    // https://www.ietf.org/rfc/rfc3066.txt

    [<Option("alang", HelpText = "Language (code ['-' region]) of the assemblies, default is 'en-US'.")>]
    AssemblyLanguage: string

    [<Option('l', "lang", Required = true, HelpText = "Language (code ['-' region]) to be translated to.")>]
    Language: string

    [<Value(0, HelpText = "Relative path of the assembly file.")>]
    Assembly: string
}

[<Verb("update", HelpText = "Update the strings from all assemblies or the specific assembly names provided.")>]
type UpdateOptions = {
    [<Value(0, HelpText = "Name of the assembly files to update. If not provided, all assemblies are updated.")>]
    Assemblies: string seq
}

[<Verb("gc", HelpText = "Remove all unused translations from the translation files.")>]
type GCOptions = {
    [<Value(0, HelpText = "Name of the assembly files to garbage collect. If not provided, all translations are garbage collected.")>]
    Assemblies: string seq
}

[<Verb("status", HelpText = "Show all the translations and their status in the current directory.")>]
type StatusOptions() = 
    class end

[<Verb("export", HelpText = "Export all strings from all translation to an XLIFF file.")>]
type ExportOptions = {
    [<Option("name", HelpText = "The XLIFF base name to generate, default is the current directory's name.")>]
    BaseName : string
    [<Value(0, Required = true, HelpText = "The output directory to export the XLIFF files to.")>]
    OutputDirectory: string
}

[<Verb("import", HelpText = "Import XLIFF translation files and apply the changes to the translations in the current directory.")>]
type ImportOptions = {
    [<Value(0, Min=1, HelpText = "The XLIFF files to import.")>]
    Files: string seq
}

let private argumentTypes = [|
    typeof<AddOptions>
    typeof<UpdateOptions>
    typeof<GCOptions>
    typeof<StatusOptions>
    typeof<ImportOptions>
    typeof<ExportOptions>
|]

let dispatch (command: obj) = 

    let currentDirectory = Directory.current()

    match command with
    | :? AddOptions as opts -> 
        API.add 
            (Language(opts.Language))
            ( opts.AssemblyLanguage |> Option.ofObj |> Option.map Language
            , opts.Assembly |> Option.ofObj |> Option.map AssemblyPath)

    | :? UpdateOptions as opts ->
        let assemblies =
            opts.Assemblies
            |> Seq.map AssemblyFilename
            |> Seq.toList

        API.update assemblies

    | :? GCOptions as opts ->
        let assemblies =
            opts.Assemblies
            |> Seq.map AssemblyFilename
            |> Seq.toList

        API.gc assemblies

    | :? StatusOptions ->
        API.status()

    | :? ExportOptions as opts ->

        let baseName = 
            opts.BaseName
            |> Option.ofObj
            |> Option.defaultWith 
                ^ fun () -> Path.name currentDirectory
            |> XLIFFBaseName

        let outputDirectory = Path.parse opts.OutputDirectory

        API.export baseName outputDirectory

    | :? ImportOptions as opts ->
        let xlfFilePaths =
            opts.Files
            |> Seq.map ^ fun file -> currentDirectory |> Path.extend file
            |> Seq.toList
        API.import xlfFilePaths

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
