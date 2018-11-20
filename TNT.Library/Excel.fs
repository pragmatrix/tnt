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
    ws.RowHeight <- 0.

    ignore ^ ws.Protect().SetFormatColumns().SetFormatRows()

    ws.Cell(1,1).Value <- sprintf "Original %s" (string file.SourceLanguage)
    ws.Cell(1,2).Value <- sprintf "Translated %s" (string file.TargetLanguage)
    ws.Cell(1,3).Value <- sprintf "State"
    ws.Cell(1,4).Value <- sprintf "Context"
    ws.Cell(1,5).Value <- sprintf "Notes"
    file.TranslationUnits
    |> Seq.iteri ^ fun i tu ->
        let row = i + 2

        do
            let sourceCell = ws.Cell(row, 1)
            sourceCell.Value <- tu.Source

            sourceCell.DataType <- XLDataType.Text
            sourceCell.Style.Alignment.WrapText <- false
        
        do
            let targetCell = ws.Cell(row, 2)
            targetCell.Value <- tu.Target
            targetCell.DataType <- XLDataType.Text
            targetCell.Style.Alignment.WrapText <- false

            ignore ^ targetCell.Style.Protection.SetLocked(false)

        do
            let stateCell = ws.Cell(row, 3)
            stateCell.Value <- string tu.State
            stateCell.DataValidation.List(AllStates, true)
            ignore ^ stateCell.Style.Protection.SetLocked(false)
        
        ws.Cell(row, 4).Value <- tu.Contexts |> String.concat "\n"
        ws.Cell(row, 5).Value <- tu.Notes |> String.concat "\n"

        // this enables proper auto-sizing
        // https://github.com/ClosedXML/ClosedXML/issues/934
        //ws.Row(row) (*.AdjustToContents(). *)ClearHeight()


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

