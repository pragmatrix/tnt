/// XLIFF export and import
module TNT.Library.XLIFF

open System.IO
open System.Text
open System.Xml
open System.Xml.Linq
open System.Security.Cryptography
open FunToolbox.FileSystem
open TNT.Model
open TNT.Library.ExportModel

// Tag related to XLIFF
[<Struct>]
type XLIFF = XLIFF

/// The supported for XLIFF files. The first one ".xlf" is the default extension
/// used in the exporting process.
let Extensions = [ ".xlf"; ".xliff" ]
let DefaultExtension = Extensions |> List.head

// VisualStudio uses the dot extension to separate the identifier from the base name.
let [<Literal>] IdentifierSeparator = "."

let defaultFilenameForLanguage (project: ProjectName) (LanguageTag(identifier)) : XLIFF filename =
    Filename ^ string project + IdentifierSeparator + identifier + DefaultExtension

module TargetState = 

    let toString = function
        | New -> "new"
        | NeedsReview -> "needs-review-translation"
        | Translated -> "translated"
        | Final -> "final"

    let tryParse = function
        | "new" -> Ok New
        | "needs-review-translation" -> Ok NeedsReview
        | "translated" -> Ok Translated
        | "final" -> Ok Final
        | str  -> Error str

type XLIFFV12 = 
    | XLIFFV12 of string
    override this.ToString() = 
        this |> function XLIFFV12 str -> str

let [<Literal>] Version = "1.2"
let [<Literal>] Namespace = "urn:oasis:names:tc:xliff:document:1.2"
let [<Literal>] SourceFileDatatype = "x-net-assembly"
let [<Literal>] WarningPrefix = "Warning:"
let [<Literal>] ContextPrefix = "Context:"
let IgnorableNotePrefixes = [| ContextPrefix; WarningPrefix |]

let private canNoteBeIgnored (note: string) = 
    IgnorableNotePrefixes 
    |> Array.exists ^ fun prefix -> note.startsWith prefix

module private Hash =
    open System.Text

    let private sha256 = new SHA256Managed()

    let ofString (str: string) = 
        let bytes = Encoding.UTF8.GetBytes(str)
        sha256.ComputeHash(bytes)
        |> Seq.map ^ sprintf "%x"
        |> String.concat ""

module private X =

    let ns str = XNamespace.op_Implicit str
    let name str = XName.op_Implicit str

/// Generate XLIFF version 1.2
let generateV12 (format: XLIFFFormat) (files: File list) : XLIFFV12 =

    let en (name: string) (ns: string) (nested: obj list) = 
        XElement(X.ns ns + name, nested)

    let e (name: string) (nested: obj list) = 
        en name Namespace nested

    let a (name: string) (value: obj) =
        XAttribute(X.name name, value)
    
    let l (l: 'a list) = List.map box l

    let preserveSpace = 
        XAttribute(XNamespace.Xml + "space", "preserve")

    let root = e "xliff" [
        yield a "version" Version
        for file in files -> e "file" [
            yield! l [
                a "original" (string file.ProjectName)
                a "source-language" (string file.SourceLanguage)
                a "target-language" (string file.TargetLanguage)
                a "datatype" SourceFileDatatype
            ]
            yield e "body" [
                let units = l [
                    for unit in file.TranslationUnits -> e "trans-unit" [
                        yield a "id" (Hash.ofString unit.Source)
                        // this resource should be translated.
                        yield a "translate" "yes"
                        yield preserveSpace
                        yield e "source" [ XText(unit.Source) ]
                        yield e "target" [
                            a "state" (TargetState.toString unit.State) 
                            XText(unit.Target) 
                        ]
                        let allNotes = List.collect id [
                            unit.Warnings |> List.map ^ fun str -> WarningPrefix + " " + str
                            unit.Contexts |> List.map ^ fun str -> ContextPrefix + " " + str
                            unit.Notes
                        ]
                        for note in allNotes ->
                            e "note" [ XText note ]
                    ]
                ]

                if format.RequiresGroups then
                    // Although optional, Multilingual App Toolkit for Windows requires <group> for loading
                    // _and_ the id attribute for saving the xliff properly.
                    yield e "group" [
                        yield a "id" (Hash.ofString ^ string file.ProjectName)
                        yield! units
                    ]
                else
                    yield! units
            ]
        ]
    ]
    
    XLIFFV12 ^ XDocument(root).ToString()

let private nsName name = X.ns Namespace + name

type XElement with

    member e.PosInfo : string = 
        let li = e :> IXmlLineInfo
        if li.HasLineInfo() 
        then sprintf "line %d, column %d" li.LineNumber li.LinePosition
        else "(no position info)"

    member element.tryGetValue (attributeName: string) : string option =
        let attr = element.Attribute(X.name attributeName)
        match attr with
        | null -> None
        | attr -> Some attr.Value

    member element.getValue (attributeName: string) : string = 
        let v = element.tryGetValue attributeName
        match v with
        | Some value -> value
        | None ->
            failwithf "required attribute '%s' not available in element '%s' at %s" 
                attributeName element.Name.LocalName (element.PosInfo)

    member element.oneNested (name: string) : XElement =
        let nested = element.Nested name
        if nested.Length <> 1 then   
            failwithf "%s: expect exactly one element '%s' under '%s'" element.PosInfo name element.Name.LocalName
        nested.[0]

    member element.Nested (name: string) : XElement list = 
        element.Elements(nsName name) |> Seq.toList

    member element.Text : string = 
        element.Nodes()
        |> Seq.choose ^ function :? XText as xt -> Some xt | _ -> None
        |> Seq.map ^ fun tn -> tn.Value
        |> String.concat ""

let parseV12 (XLIFFV12 xliff) : File list = 
    let document = XDocument.Parse(xliff, LoadOptions.SetLineInfo)
    let root = document.Root
    do
        if (root.Name <> nsName "xliff") then
            failwithf "unexpected root element <%s>, expected <xliff>" root.Name.LocalName
        let version = root.getValue "version"
        if version <> Version then
            failwithf "XLIFF version '%s', expected '%s'" version Version

    let files = 
        nsName "file"
        |> root.Elements

    let parseFile (file: XElement) =
        let name = 
            file.getValue "original"
            |> ProjectName
        
        let sourceLanguage = 
            file.getValue "source-language"
            |> LanguageTag

        let targetLanguage = 
            file.getValue "target-language" 
            |> LanguageTag

        let units = 
            let shouldTranslate (element: XElement) = 
                element.tryGetValue "translate"
                |> Option.defaultValue "yes"
                |> (=) "yes"

            nsName "trans-unit"
            |> file.Descendants
            |> Seq.filter ^ shouldTranslate
            |> Seq.map ^ fun tu ->
                let source, target, notes = 
                    tu.oneNested "source", 
                    tu.oneNested "target",
                    tu.Nested "note" |> List.map ^ fun e -> e.Text

                target.getValue "state"
                |> TargetState.tryParse
                |> function
                    | Error str ->
                        failwithf "%s: unsupported target state '%s'" target.PosInfo str 
                    | Ok state -> {
                        Source = source.Text
                        Target = target.Text
                        State = state
                        Warnings = []
                        Contexts = []
                        Notes = notes |> List.filter (not << canNoteBeIgnored)
                    }
            |> Seq.toList

        {
            SourceLanguage = sourceLanguage
            ProjectName = name
            TargetLanguage = targetLanguage
            TranslationUnits = units
        }

    files
    |> Seq.map parseFile
    |> Seq.toList
    
let exporter (format: XLIFFFormat) = {
    Extensions = Extensions
    DefaultExtension = DefaultExtension
    FilenameForLanguage = defaultFilenameForLanguage
    ExportToPath = fun path file ->
        let (XLIFFV12 generated) = generateV12 format [file]
        File.saveText Encoding.UTF8 generated path
}

let projectPatterns (project: ProjectName) : GlobPattern list =
    Extensions
    |> List.map ^ fun ext ->
        GlobPattern(string project + "*" + ext)

/// Returns ARPath of the file if the given path is most likely a XLIFF or XLF file.
let properPath (path: string) : ARPath option = 
    Extensions
    |> Seq.tryFind path.endsWith
    |> Option.map ^ fun _ -> ARPath.parse path
        
/// Get all the XLIFF files in the directory baseName.
let filesInDirectory (directory: Path) (project: ProjectName) : XLIFF filename list =
    projectPatterns project
    |> Seq.collect ^ fun pattern ->
        Directory.EnumerateFiles (string directory, string ^ pattern)
    |> Seq.map (Path.parse >> Path.name >> Filename)
    |> Seq.toList
