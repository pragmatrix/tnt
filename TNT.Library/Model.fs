namespace TNT.Model

/// Project name, currently the name of the current directory.
[<Struct>]
type ProjectName = 
    | ProjectName of string
    override this.ToString() = 
        this |> function ProjectName str -> str

/// A glob pattern "*": any substring, "?": any character
[<Struct>]
type GlobPattern = 
    | GlobPattern of string
    override this.ToString() =
        this |> function GlobPattern str -> str

/// The filename of an assembly.
[<Struct>]
type AssemblyFilename = 
    | AssemblyFilename of string
    override this.ToString() = 
        this |> function AssemblyFilename str -> str

/// A relative path to the assembly.
[<Struct>] 
type AssemblyPath = 
    | AssemblyPath of string
    override this.ToString() = 
        this |> function AssemblyPath str -> str

/// An IETF language tag. https://en.wikipedia.org/wiki/IETF_language_tag
[<Struct>]
type LanguageTag = 
    | LanguageTag of string
    override this.ToString() = 
        this |> function LanguageTag tag -> tag
    /// The primary language subtag.
    member this.Primary =
        string this
        |> fun str -> str.Split('-')
        |> Array.head
        |> LanguageTag

/// The filename of a translation file.
[<Struct>]
type TranslationFilename = 
    | TranslationFilename of string
    override this.ToString() = 
        this |> function TranslationFilename str -> str

/// The filename of an XLIFF file.
[<Struct>]
type XLIFFFilename = 
    | XLIFFFilename of string
    override this.ToString() = 
        this |> function XLIFFFilename str -> str

[<RQA>]
type TranslatedString = 
    /// A newly detected string of which the translation is empty.
    | New 
    /// For example, a machine translated version.
    | NeedsReview of string 
    /// The translation has been reviewed and is good to go.
    | Final of string 
    /// This translation is unused right now and should be garbage collected. We store
    /// the previous string for cases in which the source string will reappear.
    | Unused of string 
    override this.ToString() =
        match this with
        | New -> ""
        | NeedsReview str
        | Final str
        | Unused str -> str

[<Struct>]
type TranslationRecord = {
    Original: string
    Translated: TranslatedString
}

/// Information about an assembly.
[<Struct>]
type AssemblyInfo = {
    Language: LanguageTag
    Path: AssemblyPath
} with
    override this.ToString() = 
        sprintf "[%s]%s" (string this.Language) (string this.Path)

/// The original strings extracted. 
/// The strings are sorted and duplicates are removed.
[<Struct>]
type OriginalStrings =
    private OriginalStrings of strings: Set<string>

module OriginalStrings = 
    let create strings = 
        OriginalStrings(Set.ofSeq strings)
    let strings (OriginalStrings(strings)) = strings |> Set.toList
    let merge list = 
        list 
        |> Seq.collect strings
        |> create



/// A source of strings.
type Source = 
    | AssemblySource of AssemblyPath

/// A defininiton of sources.
type Sources = {
    Language: LanguageTag
    Sources: Set<Source>
}

/// A translation of strings.
[<Struct>]
type Translation = {
    Language: LanguageTag
    Records: TranslationRecord list
}

/// A group of translations for different langauges.
[<Struct>]
type TranslationGroup = TranslationGroup of Map<LanguageTag, Translation>

[<Struct>]
type TranslationCounters = {
    New: int
    NeedsReview: int
    Final: int
    Unused: int
} with
    override this.ToString() = 
        [
            "n", this.New
            "r", this.NeedsReview
            "f", this.Final
            "u", this.Unused 
        ]
        |> Seq.filter ^ fun (_, v) -> v <> 0
        |> Seq.map ^ fun (indicator, v) -> string v + indicator
        |> String.concat ","
