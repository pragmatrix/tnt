open System
open TNT.Model
open TNT.Library
open TNT.Library.Output
open FunToolbox.FileSystem
open CommandLine

[<Verb("add", HelpText = "Add a new language to all existing translations or to one specific assembly.")>]
type AddOptions = {
    // http://www.i18nguy.com/unicode/language-identifiers.html
    // https://www.ietf.org/rfc/rfc3066.txt

    [<Option("alang", HelpText = "Language identifier (code ['-' region]) of the language of the assemblies, default is 'en-US'.")>]
    AssemblyLanguage: string

    [<Option('l', "lang", Required = true, HelpText = "Language identifier (code ['-' region]) of the language to be translated to.")>]
    Language: string

    [<Value(0, HelpText = "Relative path of the assembly file.")>]
    Assembly: string
}

[<Verb("update", HelpText = "Extract all strings from all assemblies or one specific assembly and update the translation files.")>]
type UpdateOptions = {
    [<Value(0, HelpText = "Relative path of the asseembly file.")>]
    Assembly: string
}

[<Verb("status", HelpText = "Show all the translations and their status in the current directory.")>]
type StatusOptions() = 
    class end

[<Verb("export", HelpText = "Export all strings from all translation to an XLIFF file.")>]
type ExportOptions = {
    [<Option("srcLang", HelpText = "Language identifier (code ['-' region]) of the language translated from, default is 'en-US'.")>]
    SourceLanguage: string
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
    typeof<StatusOptions>
    typeof<ImportOptions>
    typeof<ExportOptions>
|]

let dispatch (command: obj) = 

    let currentDirectory = Directory.current()

    match command with
    | :? AddOptions as opts -> 
        API.add 
            (LanguageIdentifier(opts.Language))
            ( opts.AssemblyLanguage |> Option.ofObj |> Option.map LanguageIdentifier
            , opts.Assembly |> Option.ofObj |> Option.map AssemblyPath)

    | :? UpdateOptions as opts ->
        API.update
            (opts.Assembly |> Option.ofObj |> Option.map AssemblyPath)

    | :? StatusOptions ->
        API.status()

    | :? ExportOptions as opts ->

        let sourceLanguage =
            opts.SourceLanguage
            |> Option.ofObj
            |> Option.defaultValue "en-US"
            |> LanguageIdentifier

        let baseName = 
            opts.BaseName
            |> Option.ofObj
            |> Option.defaultWith 
                ^ fun () -> Path.name currentDirectory
            |> XLIFFBaseName

        let outputDirectory = Path.parse opts.OutputDirectory

        API.export sourceLanguage baseName outputDirectory

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
