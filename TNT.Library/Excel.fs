module TNT.Library.Excel

open System
open System.IO
open FunToolbox.FileSystem
open ClosedXML.Excel
open TNT.Model
open TNT.Library.ExportModel
open TNT.Library

/// Tag and transport structure for an excel file.
[<Struct>]
type Excel = Excel of byte[]

module TargetState = 
    let toString = function
        | New 
            -> "New"
        | NeedsReview 
            -> "Needs Review"
        | Translated 
        | Final 
            -> "Final"

    let parse = function
        | "New" 
            -> New
        | "Needs Review" 
            -> NeedsReview
        | "Final" 
            -> Final
        | state 
            -> failwithf "invalid state: '%s'" state

[<AutoOpen>]
module private Private = 

    let ValidExcelStates = [
        New
        NeedsReview
        Final
    ]

    let StateValidationList = 

        let inline quote str = "\"" + str + "\""

        ValidExcelStates
        |> Seq.map TargetState.toString
        |> String.concat ","
        |> quote

    let sheetPostfix (stateName: string) = 
        " - " + stateName

let [<Literal>] SourceColumn = 1
let [<Literal>] ChangedColumn = 2
let [<Literal>] ChangedPostfix = " (changed)"
let [<Literal>] CurrentColumn = 3
let [<Literal>] CurrentPostfix = " (current)"
let [<Literal>] StateColumn = 4
let [<Literal>] ContextsColumn = 5
let [<Literal>] NotesColumn = 6

let generate (file: File<ExportUnit>) : Excel = 

    use wb = new XLWorkbook();

    let setupWorksheet (name: string) (tus: ExportUnit list) = 

        let ws = wb.Worksheets.Add(name)
        ws.Protect().AllowedElements 
            <- XLSheetProtectionElements.FormatColumns 
            ||| XLSheetProtectionElements.FormatRows

        ws.Cell(1, SourceColumn)
            .Value <- sprintf "%s" (file.SourceLanguage.Formatted)
        ws.Cell(1, ChangedColumn)
            .Value <- sprintf "%s%s" (file.TargetLanguage.Formatted) ChangedPostfix
        ws.Cell(1, CurrentColumn)
            .Value <- sprintf "%s%s" (file.TargetLanguage.Formatted) CurrentPostfix
        ws.Cell(1, StateColumn)
            .Value <- "State"
        ws.Cell(1,ContextsColumn)
            .Value <- "Contexts"
        ws.Cell(1, NotesColumn)
            .Value <- "Notes"
    
        ws.Row(1).Style.Font.Bold <- true
    
        file.TranslationUnits
        |> Seq.iteri ^ fun i tu ->
            let row = i + 2

            // source
            do
                let cell = ws.Cell(row, SourceColumn)
                cell.Value <- tu.Source
                cell.DataType <- XLDataType.Text

                cell.Style.Alignment.WrapText <- true
                cell.Style.Alignment.Vertical <- XLAlignmentVerticalValues.Center

            // changed
            do
                let cell = ws.Cell(row, ChangedColumn)
                cell.Value <- ""
                cell.DataType <- XLDataType.Text
                
                cell.Style.Alignment.WrapText <- true
                cell.Style.Alignment.Vertical <- XLAlignmentVerticalValues.Center

                ignore ^ cell.Style.Protection.SetLocked(false)

            // current     
            do
                let cell = ws.Cell(row, CurrentColumn)
                cell.Value <- tu.Target
                cell.DataType <- XLDataType.Text

                cell.Style.Alignment.WrapText <- true
                cell.Style.Alignment.Vertical <- XLAlignmentVerticalValues.Center

            // state
            do
                let cell = ws.Cell(row, StateColumn)
                cell.Value <- TargetState.toString tu.State
                cell.DataValidation.List(StateValidationList, true)

                cell.Style.Alignment.Vertical <- XLAlignmentVerticalValues.Center

                ignore ^ cell.Style.Protection.SetLocked(false)

            // contexts
            do         
                let cell = ws.Cell(row, ContextsColumn)
                cell.Value <- tu.Contexts |> String.concat "\n"
                cell.DataType <- XLDataType.Text

                cell.Style.Alignment.WrapText <- true
                cell.Style.Alignment.Vertical <- XLAlignmentVerticalValues.Center

            // notes
            do

                let cell = ws.Cell(row, NotesColumn)
                // two newlines for note separation (because notes may consist of multiple lines).
                cell.Value <- tu.Notes |> String.concat "\n\n"
                cell.DataType <- XLDataType.Text

                cell.Style.Alignment.WrapText <- true
                cell.Style.Alignment.Vertical <- XLAlignmentVerticalValues.Center

        // autosize columns.
        ignore ^ ws.Columns().AdjustToContents(10., 100.)
    
    file.TranslationUnits
    |> List.groupBy ^ fun tu -> TargetState.toString tu.State
    |> Seq.filter ^ fun (_, units) -> units <> []
    |> Seq.iter ^ fun (stateName, units) ->
        let worksheetName = string file.ProjectName + sheetPostfix stateName
        units |> setupWorksheet worksheetName

    use ms = new MemoryStream()
    wb.SaveAs(ms)
    Excel ^ ms.ToArray()

type SheetMetadata = {
    ProjectName: ProjectName
    SourceLanguage: LanguageTag
    TargetLanguage: LanguageTag
}

let parse (Excel excel) : File<ImportUnit> = 
    
    let tryGetProjectName (worksheetName: string) : ProjectName option = 
        ValidExcelStates
        |> Seq.map ^ TargetState.toString
        |> Seq.tryPick ^ fun stateName -> 
            let postfix = sheetPostfix stateName
            if worksheetName.endsWith postfix
            then Some ^ ProjectName worksheetName.[0..worksheetName.Length - postfix.Length - 1]
            else None

    let (|MatchLanguageTag|_|) (str: string) = 
        match str with
        | Regex.Match("^\[(.+)\]$") [tag] -> Some ^ LanguageTag tag
        | _ -> None

    let (|EndsWith|_|) (postfix: string) (str: string) = 
        if str.endsWith postfix 
        then Some(str.[0..str.Length-postfix.Length-1])
        else None

    let extractMetadata (ws: IXLWorksheet) : Result<SheetMetadata, string> = 
        match tryGetProjectName ws.Name with
        | None -> Error ^ sprintf "failed to extract project name from worksheet '%s'" ws.Name
        | Some projectName ->

        let sourceLanguage = 
            match ws.Cell(1, SourceColumn).TryGetValue<string>() with
            | true, MatchLanguageTag tag -> Some tag
            | _ -> None

        match sourceLanguage with
        | None -> Error ^ sprintf "failed to extract source language from worksheet '%s'" ws.Name
        | Some sourceLanguage ->

        let targetLanguage = 
            match ws.Cell(1, ChangedColumn).TryGetValue<string>() with
            | true, EndsWith ChangedPostfix tagx -> 
                match tagx with 
                | MatchLanguageTag tag -> Some ^ tag
                | _ -> None
            | _ -> None

        match targetLanguage with
        | None -> Error ^ sprintf "failed to extract target language from worksheet '%s'" ws.Name
        | Some targetLanguage ->

        Ok {
            ProjectName = projectName
            SourceLanguage = sourceLanguage
            TargetLanguage = targetLanguage
        }

    let extractUnits (ws: IXLWorksheet) : ImportUnit list =

        // We assume that rows are valid until the state entry is empty.

        let tryParseState (cell: IXLCell) : TargetState option = 
            match cell.TryGetValue<string>() with
            | true, NotEmptyTrimmed trimmed 
                -> Some ^ TargetState.parse trimmed
            | _ -> None

        { 2..Int32.MaxValue }
        |> Seq.map ^ fun row -> row, tryParseState (ws.Cell(row, StateColumn))
        |> Seq.takeWhile ^ fun (_, state) -> state <> None
        |> Seq.choose ^ fun (row, state) -> 
            match ws.Cell(row, ChangedColumn).GetValue<string>() with
            | "" -> None
            | changed -> Some {
                    Source = ws.Cell(row, SourceColumn).GetValue<string>()
                    Target = changed
                    State = state.Value
                    Notes = None
                }
        |> Seq.toList
    
    use ms = new MemoryStream(excel)
    use wb = new XLWorkbook(ms)
    
    let units =
        wb.Worksheets
        |> Seq.map ^ fun sheet ->
            sheet, (extractMetadata sheet |> function Ok r -> r | Error str -> failwith str)
        |> Seq.map ^ fun (sheet, metadata) -> metadata, extractUnits sheet
        |> Seq.toList

    match units |> List.map fst with
    | List.AllEqual (Some metadata) ->
        {
            ProjectName = metadata.ProjectName
            SourceLanguage = metadata.SourceLanguage
            TargetLanguage = metadata.TargetLanguage
            TranslationUnits = units |> List.collect snd
        }
    | _ -> failwith "Sheets contain strings from different exports."

/// Get all the Excel files in the directory baseName.
let filesInDirectory (project: ProjectName) (directory: Path) : Export filename list =
    Directory.EnumerateFiles (string directory, string project + "-*.xlsx")
    |> Seq.map (Path.parse >> Path.name >> Filename)
    |> Seq.toList

let Exporter : Exporter = 
    let defaultExtension = ".xlsx"
    {
        Extensions = [ ".xlsx" ]
        DefaultExtension = defaultExtension
        DefaultFilename = fun projectName languageTag -> 
            Filename ^ (string projectName) + "-" + (string languageTag) + defaultExtension
        SaveToPath = fun path file ->
            let (Excel bytes) = generate file           
            File.saveBinary bytes path
        LoadFromPath = fun path -> [
            File.loadBinary path
            |> Excel
            |> parse
        ]
        FilesInDirectory = filesInDirectory
    }

