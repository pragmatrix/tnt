/// XLIFF export and import
module TNT.Library.XLIFF

open TNT.Model
open System.Xml.Linq
open System.Security.Cryptography
open System.Xml

/// The state of a translation. 
/// Note: Multilingual app toolkit supports 
/// "new", "need-review-translation", "translated", and "final".
type TargetState = 
    | New
    | NeedsReview
    | Translated
    | Final
    override this.ToString() = 
        match this with
        | New -> "new"
        | NeedsReview -> "needs-review-translation"
        | Translated -> "translated"
        | Final -> "final"

module TargetState = 
    
    let tryParse = function
        | "new" -> Ok New
        | "needs-review-translation" -> Ok NeedsReview
        | "translated" -> Ok Translated
        | "final" -> Ok Final
        | str  -> Error str

type TranslationUnit = {
    Source: string
    Target: string
    State: TargetState
    Notes: string list
}

type File = {
    Name: string
    SourceLanguage: LanguageTag
    TargetLanguage: LanguageTag
    TranslationUnits: TranslationUnit list
}

type XLIFFV12 = 
    | XLIFFV12 of string
    override this.ToString() = 
        this |> function XLIFFV12 str -> str

let [<Literal>] Version = "1.2"
let [<Literal>] Namespace = "urn:oasis:names:tc:xliff:document:1.2"
let [<Literal>] SourceFileDatatype = "x-net-assembly"

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
let generateV12 (files: File list) : XLIFFV12 =

    let en (name: string) (ns: string) (nested: obj list) = 
        XElement(X.ns ns + name, nested)

    let e (name: string) (nested: obj list) = 
        en name Namespace nested

    let a (name: string) (value: obj) =
        XAttribute(X.name name, value)
    
    let l = List.map box

    let preserveSpace = 
        XAttribute(XNamespace.Xml + "space", "preserve")

    let root = e "xliff" [
        yield a "version" Version
        for file in files -> e "file" [
            yield! l [
                a "original" (string file.Name)
                a "source-language" (string file.SourceLanguage)
                a "target-language" (string file.TargetLanguage)
                a "datatype" SourceFileDatatype
            ]
            yield e "body" [
                // Although optional, Multilingual App Toolkit for Windows requires <group> for loading
                // _and_ the id attribute for saving the xliff properly.
                e "group" [
                    yield a "id" (Hash.ofString ^ string file.Name)
                    for unit in file.TranslationUnits -> e "trans-unit" [
                        yield a "id" (Hash.ofString unit.Source)
                        // this resource should be translated.
                        yield a "translate" "yes"
                        yield preserveSpace
                        yield e "source" [ XText(unit.Source) ]
                        yield e "target" [
                            a "state" (string unit.State) 
                            XText(unit.Target) 
                        ]
                        for note in unit.Notes ->
                            e "note" [ XText note ]
                    ]
                ]
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
                        Notes = notes
                    }
            |> Seq.toList

        {
            SourceLanguage = sourceLanguage
            Name = name
            TargetLanguage = targetLanguage
            TranslationUnits = units
        }

    files
    |> Seq.map parseFile
    |> Seq.toList
    