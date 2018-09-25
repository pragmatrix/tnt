/// XLIFF export and import
module TNT.Library.XLIFF

open System
open TNT.Model
open System.Xml.Linq
open System.Security.Cryptography

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

/// Generate XLIFF version 1.2
let generateV12
    (sourceLanguage: LanguageIdentifier) 
    (files: File list) : XLIFFV12 =

    let en (name: string) (ns: string) (nested: obj list) = 
        let ns = XNamespace.op_Implicit ns
        XElement(ns + name, nested)

    let e (name: string) (nested: obj list) = 
        en name Namespace nested

    let a (name: string) (value: obj) =
        XAttribute(XName.op_Implicit name, value)
    
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