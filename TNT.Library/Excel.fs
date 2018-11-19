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
    wb.Protect()

    let ws = wb.Worksheets.Add(string file.ProjectName)
    ws.Cell(1,1).Value <- sprintf "Original %s" (string file.SourceLanguage)
    ws.Cell(1,2).Value <- sprintf "Translated %s" (string file.TargetLanguage)
    ws.Cell(1,3).Value <- sprintf "State"
    ws.Cell(1,4).Value <- sprintf "Context"
    ws.Cell(1,5).Value <- sprintf "Notes"

    file.TranslationUnits
    |> Seq.iteri ^ fun i tu ->
        let row = i + 2
        ws.Cell(row, 1).Value <- tu.Source

        do
            let targetCell = ws.Cell(row, 2)
            targetCell.Value <- tu.Target
            ignore ^ targetCell.Style.Protection.SetLocked(false)

        do
            let stateCell = ws.Cell(row, 3)
            stateCell.Value <- string tu.State
            stateCell.DataValidation.List(AllStates, true)
            ignore ^ stateCell.Style.Protection.SetLocked(false)
        
        ws.Cell(row, 4).Value <- tu.Contexts |> String.concat "\n"
        ws.Cell(row, 5).Value <- tu.Notes |> String.concat "\n"

    use ms = new MemoryStream()
    wb.SaveAs(ms)
    Excel ^ ms.ToArray()

let Exporter : Exporter<Excel> = 
    let defaultExtension = ".xls"
    {
        Extensions = [ ".xls" ]
        DefaultExtension = defaultExtension
        FilenameForLanguage = fun projectName languageTag -> 
            Filename ^ (string projectName) + "-" + (string languageTag) + defaultExtension
        ExportToPath = fun path file ->
            let (Excel bytes) = export file           
            File.saveBinary bytes path
    }

