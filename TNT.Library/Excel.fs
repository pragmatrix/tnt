module TNT.Library.Excel

open System.IO
open FunToolbox.FileSystem
open ClosedXML.Excel
open TNT.Library.ExportModel

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
            -> "Translated"
        | Final 
            -> "Final"

    let All = [
        New
        NeedsReview
        Translated
        Final
    ]

[<AutoOpen>]
module private Private = 

    let AllStates = 

        let inline quote str = "\"" + str + "\""

        TargetState.All
        |> Seq.map TargetState.toString
        |> String.concat ","
        |> quote

let export (file: File) : Excel = 

    use wb = new XLWorkbook();

    let ws = wb.Worksheets.Add(string file.ProjectName)

    ignore ^ ws.Protect().SetFormatColumns().SetFormatRows()

    ws.Cell(1,1).Value <- sprintf "%s" (file.SourceLanguage.Formatted)
    ws.Cell(1,2).Value <- sprintf "%s" (file.TargetLanguage.Formatted)
    ws.Cell(1,3).Value <- sprintf "State"
    ws.Cell(1,4).Value <- sprintf "Context"
    ws.Cell(1,5).Value <- sprintf "Notes"
    
    ws.Row(1).Style.Font.Bold <- true
    
    file.TranslationUnits
    |> Seq.iteri ^ fun i tu ->
        let row = i + 2

        // source
        do
            let cell = ws.Cell(row, 1)
            cell.Value <- tu.Source
            cell.DataType <- XLDataType.Text

            cell.Style.Alignment.WrapText <- true
            cell.Style.Alignment.Vertical <- XLAlignmentVerticalValues.Center

        // target        
        do
            let cell = ws.Cell(row, 2)
            cell.Value <- tu.Target
            cell.DataType <- XLDataType.Text

            cell.Style.Alignment.WrapText <- true
            cell.Style.Alignment.Vertical <- XLAlignmentVerticalValues.Center

            ignore ^ cell.Style.Protection.SetLocked(false)

        // state
        do
            let cell = ws.Cell(row, 3)
            cell.Value <- TargetState.toString tu.State
            cell.DataValidation.List(AllStates, true)

            cell.Style.Alignment.Vertical <- XLAlignmentVerticalValues.Center

            ignore ^ cell.Style.Protection.SetLocked(false)

        // contexts
        do         
            let cell = ws.Cell(row, 4)
            cell.Value <- tu.Contexts |> String.concat "\n"
            cell.DataType <- XLDataType.Text

            cell.Style.Alignment.WrapText <- true
            cell.Style.Alignment.Vertical <- XLAlignmentVerticalValues.Center

        // notes
        do

            let cell = ws.Cell(row, 5)
            // two newlines for note separation (because notes may consist of multiple lines).
            cell.Value <- tu.Notes |> String.concat "\n\n"
            cell.DataType <- XLDataType.Text

            cell.Style.Alignment.WrapText <- true
            cell.Style.Alignment.Vertical <- XLAlignmentVerticalValues.Center

    // autosize columns.
    ignore ^ ws.Columns().AdjustToContents(10., 200.)
    
    use ms = new MemoryStream()
    wb.SaveAs(ms)
    Excel ^ ms.ToArray()

let Exporter : Exporter<Excel> = 
    let defaultExtension = ".xlsx"
    {
        Extensions = [ ".xlsx" ]
        DefaultExtension = defaultExtension
        FilenameForLanguage = fun projectName languageTag -> 
            Filename ^ (string projectName) + "-" + (string languageTag) + defaultExtension
        ExportToPath = fun path file ->
            let (Excel bytes) = export file           
            File.saveBinary bytes path
    }

