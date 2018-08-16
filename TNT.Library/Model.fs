module TNT.Model

/// The Filename of an assembly.
type AssemblyFilename = 
    | AssemblyFilename of string
    override this.ToString() = 
        this |> function AssemblyFilename str -> str

/// The Filename of a translation file.
type TranslationFilename = 
    | TranslationFilename of string
    override this.ToString() = 
        this |> function TranslationFilename str -> str

/// A relative path to the assembly.
type AssemblyPath = 
    | AssemblyPath of string
    override this.ToString() = 
        this |> function AssemblyPath str -> str

type LanguageCode = 
    | LanguageCode of string
    override this.ToString() = 
        this |> function LanguageCode code -> code

/// Strings extracted from a given assembly. The original strings that are used as keys for the translations.
type ExtractedStrings = 
    | ExtractedStrings of assembly: AssemblyPath * strings: string list

type TranslatedStringState = 
    | New /// A newly detected string of which the translation is empty.
    | Auto /// A machine translated version.
    | Verified /// The translation has been verified.
    | Unused /// This translation is unused right now and should be garbage collected.
    override this.ToString() = 
        match this with
        | New -> "new"
        | Auto -> "auto"
        | Verified -> "verified"
        | Unused -> "unused"

type TranslatedString = 
    | TranslatedString of state: TranslatedStringState * original: string * translated: string

type TranslationId = 
    | TranslationId of code: LanguageCode * assembly: AssemblyPath

/// A translation of an assembly.
type Translation = 
    | Translation of TranslationId * TranslatedString list

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
    /// Initialize by given an relative assembly path, like bin/Release.
    /// Extracts the strings and creates the first language file.
    /// If no assembly path is given, it looks for all assemblies
    /// with an existing language code in the current directory and 
    /// tries to generate the new language. 
    /// Languages are removed by just deleting the files.
    | Add of language: LanguageCode * AssemblyPath option
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
