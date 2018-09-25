namespace TNT.Model

/// The filename of an assembly.
type AssemblyFilename = 
    | AssemblyFilename of string
    override this.ToString() = 
        this |> function AssemblyFilename str -> str

/// The filename of a translation file.
type TranslationFilename = 
    | TranslationFilename of string
    override this.ToString() = 
        this |> function TranslationFilename str -> str

/// The base name of an XLIFF file.
type XLIFFBaseName =
    | XLIFFBaseName of string
    override this.ToString() = 
        this |> function XLIFFBaseName str -> str

/// A relative path to the assembly.
type AssemblyPath = 
    | AssemblyPath of string
    override this.ToString() = 
        this |> function AssemblyPath str -> str

type LanguageIdentifier = 
    | LanguageIdentifier of string
    override this.ToString() = 
        this |> function LanguageIdentifier identifier -> identifier

/// Strings extracted from a given assembly. The original strings that are used as keys for the translations.
type ExtractedStrings = 
    | ExtractedStrings of assembly: AssemblyPath * strings: string list

[<RQA; Struct>]
type TranslatedStringState = 
    | New /// A newly detected string of which the translation is empty.
    | Auto /// A machine translated version.
    | Reviewed /// The translation has been reviewed and is good to go.
    | Unused /// This translation is unused right now and should be garbage collected.
    override this.ToString() = 
        match this with
        | New -> "new"
        | Auto -> "auto"
        | Reviewed -> "reviewed"
        | Unused -> "unused"

[<Struct>]
type OriginalString = 
    | OriginalString of string
    override this.ToString() = 
        this |> function OriginalString str -> str

[<Struct>]
type TranslatedString = 
    | TranslatedString of state: TranslatedStringState * string

[<Struct>]
type TranslationRecord = 
    | TranslationRecord of 
        original: OriginalString
        * translated: TranslatedString

type TranslationId = 
    | TranslationId of path: AssemblyPath * identifier: LanguageIdentifier

/// A translation of an assembly.
type Translation = 
    | Translation of id: TranslationId * records: TranslationRecord list

/// A translation set is a set of translations that 
/// all have different language identifiers and point to the same assembly path.

type TranslationSet = 
    | TranslationSet of assembly: AssemblyPath * set: Map<LanguageIdentifier, Translation>

/// An absolute path to the directory where translations are in.
type TranslationDirectory = 
    | TranslationDirectory of string
    override this.ToString() = 
        this |> function TranslationDirectory dir -> dir

/// A translation group is a group of translations that can be stored inside
/// _one_ directory. This means that only one translation for a langauge identifier
/// can exist for one AssemblyFileName.
type TranslationGroup = 
    | TranslationGroup of Map<AssemblyFilename, TranslationSet>
    
type MachineTranslationService = 
    | Google
    | Microsoft

type Undefined = exn

type MachineTranslator = Undefined
type MachineTranslationCredentials = Undefined

//
// Functions
//

/// Extract string from an assembly.
type ExtractStrings = AssemblyPath -> byte[] -> ExtractedStrings

/// Updates an existing translation file.
type UpdateTranslation = ExtractedStrings -> Translation -> Translation

/// Machine translates new texts.
type AutoTranslate = MachineTranslator -> Translation -> Translation

/// Garbage collect a translation.
type CollectGarbage = Translation -> Translation

type Command = 
    /// Extracts the strings and creates the first language file.
    /// If no assembly path is given, it looks for all assemblies
    /// with an existing language code in the current directory and 
    /// tries to generate the new language. 
    /// Languages are removed by just deleting the files.
    | Add of language: LanguageIdentifier * AssemblyPath option
    /// Extracts all strings and updates all machine translations in the current directory.
    | Update
    /// Garbage collects all translations in the current directory.
    | GC
    /// Sets the current machine translator that should be used for new strings.
    | SetMachineTranslator of MachineTranslationService * MachineTranslationCredentials
    /// Export all tnt files to xliff files
    | Export
    /// Import xliff files and apply the translations
    | Import
