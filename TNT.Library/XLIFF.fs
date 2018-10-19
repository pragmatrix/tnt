/// XLIFF export and import
module TNT.Library.XLIFF

open System
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
    TargetState: TargetState
}

type File = {
    Name: AssemblyFilename
    TargetLanguage: LanguageIdentifier
    TranslationUnits: TranslationUnit list
}

type XLIFFV12 = 
    | XLIFFV12 of string
    override this.ToString() = 
        this |> function XLIFFV12 str -> str

module Files = 

    /// Convert a translation group to a list of files.
    let fromTranslations translations = 

        let toUnit (TranslationRecord(OriginalString(original), TranslatedString(state, translated))) =

            // note that not all records can be exported into XIFF files.
            state
            |> function
            | TranslatedStringState.New -> Some New
            | TranslatedStringState.Auto -> Some NeedsReview
            | TranslatedStringState.Reviewed -> Some Final
            | TranslatedStringState.Unused -> None
            |> Option.map ^ fun state ->
            {
                Source = original
                Target = translated
                TargetState = state
            }

        let toFile (Translation(TranslationId(path, language), records)) = {
            Name = AssemblyFilename.ofPath path
            TargetLanguage = language
            TranslationUnits = records |> List.choose toUnit
        }

        translations
        |> List.map toFile

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
let generateV12
    (sourceLanguage: LanguageIdentifier) 
    (files: File list) : XLIFFV12 =

    let en (name: string) (ns: string) (nested: obj list) = 
        XElement(X.ns ns + name, nested)

    let e (name: string) (nested: obj list) = 
        en name Namespace nested

    let a (name: string) (value: obj) =
        XAttribute(X.name name, value)
    
    let l = List.map box

    let preserveSpace = 
        XAttribute(XNamespace.Xml + "space", "preserve")

    let root = 
        e "xliff" [
            yield a "version" Version
            for file in files do
                yield e "file" [
                    yield! l [
                        a "original" (string file.Name)
                        a "source-language" (string sourceLanguage)
                        a "target-language" (string file.TargetLanguage)
                        a "datatype" SourceFileDatatype
                    ]
                    yield e "body" [
                        // Although optional, Multilingual App Toolkit for Windows requires <group> for loading
                        // _and_ the id attribute for saving the xliff propertly.
                        yield e "group" [
                            yield a "id" (Hash.ofString ^ string file.Name)
                            for unit in file.TranslationUnits do
                                yield e "trans-unit" [
                                    a "id" (Hash.ofString unit.Source)
                                    // this resource should be translated.
                                    a "translate" "yes"
                                    preserveSpace
                                    e "source" [ XText(unit.Source) ]
                                    e "target" [
                                        a "state" (string unit.TargetState) 
                                        XText(unit.Target) 
                                    ]
                                ]
                        ]
                    ]
                ]

        ]
    
    XLIFFV12 ^ XDocument(root).ToString()

let parseV12 (XLIFFV12 xliff) : File list = 
    let document = XDocument.Parse(xliff, LoadOptions.SetLineInfo)
    let root = document.Root
    if (root.Name.LocalName <> "xliff") then
        failwithf "root element not found, expected 'xliff', seen '%s'" root.Name.LocalName
    let version = root.Attribute(X.name "version").Value
    if version <> Version then
        failwithf "unexpected XLIFF version, expected '%s', seen '%s'" Version version
    
    let x = root.Elements()

    let nsName name = X.ns Namespace + name

    let files = root.Elements(nsName "file")

    let posInfo (e: XElement) : string = 
        let li = e :> IXmlLineInfo
        if li.HasLineInfo() 
        then sprintf "line %d, column %d" li.LineNumber li.LinePosition
        else "(no position info)"

    let tryGetValue (element: XElement) (attributeName: string) : string option =
        let attr = element.Attribute(X.name attributeName)
        match attr with
        | null -> None
        | attr -> Some attr.Value

    let getValue (element: XElement) (attributeName: string) : string = 
        let v = tryGetValue element attributeName
        match v with
        | Some value -> value
        | None ->
            failwithf "required attribute '%s' not available in element '%s' at %s" 
                attributeName element.Name.LocalName (posInfo element)

    let oneNested (element: XElement) (name: string) : XElement =
        let nested = element.Elements(nsName name) |> Seq.toArray
        if nested.Length <> 1 then   
            failwithf "%s: expect exactly one element '%s' under '%s'" (posInfo element) name element.Name.LocalName
        nested.[0]

    let getText (element: XElement) : string = 
        element.Nodes()
        |> Seq.choose ^ function :? XText as xt -> Some xt | _ -> None
        |> Seq.map ^ fun tn -> tn.Value
        |> String.concat ""

    files
    |> Seq.map ^ fun file ->
        let name = getValue file "original" |> AssemblyFilename
        let targetLanguage = getValue file "target-language" |> LanguageIdentifier

        let units = 
            let shouldTranslate (element: XElement) = 
                tryGetValue element "translate"
                |> Option.defaultValue "yes"
                |> (=) "yes"

            file.Descendants(nsName "trans-unit")
            |> Seq.filter ^ shouldTranslate
            |> Seq.map ^ fun file ->
                let source = oneNested file "source"
                let target = oneNested file "target"
                let targetState = 
                    getValue target "state"
                    |> TargetState.tryParse
                match targetState with
                | Error str ->
                    failwithf "%s: unsupported target state '%s'" (posInfo target) str 
                | Ok state -> {
                    Source = getText source
                    Target = getText target
                    TargetState = state
                }
            |> Seq.toList

        {
            Name = name
            TargetLanguage = targetLanguage
            TranslationUnits = units
        }
            
    |> Seq.toList
    