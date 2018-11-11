module TNT.Library.Analysis

open System
open System.Text.RegularExpressions
open Chiron
open TNT.Model

type Warning = 
    | OriginalStringEmpty
    | NoTranslation
    | DifferentNumberOfLines of linesNumbers: (int * int)
    // | DifferentWhitespaceLine of line: int * (string * string)
    | DifferentWhitespaceLeft of line: int * indents: (string * string)
    | DifferentWhitespaceRight of line: int * indents: (string * string)
    /// Note that the placeholders are compared in sorted order. Which means that
    /// the order does not matter, but the the same placeholders must appear in the
    /// original and translated strings.
    | MismatchedPlaceholders of placeholders: (string list * string list)
    override this.ToString() =

        let inline formatString str = 
            str |> String |> Json.format

        match this with
        | OriginalStringEmpty 
            -> "original string is empty"
        | NoTranslation 
            -> "no translation"
        | DifferentNumberOfLines(lo, lt) 
            -> sprintf "number of lines differ: %d -> %d" lo lt
        // | DifferentWhitespaceLine(l, (ol, tl))
        //      -> sprintf "empty line %d differs in whitespace content: %s -> %s"
        //          l (formatString ol) (formatString tl)
        | DifferentWhitespaceLeft(l, (io, it))
            -> sprintf "whitespace on the left differs in line %d: %s -> %s" 
                l (formatString io) (formatString it)
        | DifferentWhitespaceRight(l, (io, it))
            -> sprintf "whitespace on the right differs in line %d: %s -> %s" 
                l (formatString io) (formatString it)
        | MismatchedPlaceholders(o, t)
            -> sprintf "set of placeholders differ: %s -> %s"
                (o |> String.concat ",")
                (t |> String.concat ",")

[<AutoOpen>]
module internal Helper = 

    let lines (str: string) : int = 
        str 
        |> Seq.sumBy ^ fun c -> if c = '\n' then 1 else 0
        |> (+) 1

    /// Support string.Format placeholders only, because F# placeholders are not suited
    /// for translations, because their position must stay the same.
    /// (we could verify that, too I guess).
    /// Note (?: introduced a non-capturing group).
    let [<Literal>] PlaceholderPattern = @"{\d+(?:,[+-]?\d+)?(?::[^\n{}]+)?}"

    let PlaceholderRegex = new Regex(PlaceholderPattern, RegexOptions.Compiled ||| RegexOptions.CultureInvariant)

    let placeholders (str: string) : string list =
        PlaceholderRegex.Matches(str)
        |> Seq.cast<Match>
        |> Seq.map ^ fun m -> 
            assert(m.Success)
            m.Value
        |> Seq.toList

    let analyzePlaceholders (original: string, translated: string) : Warning list = [
        let original, translated = 
            List.sort ^ placeholders original,
            List.sort ^ placeholders translated
        if original <> translated then
            yield MismatchedPlaceholders(original, translated)
    ]

    let whitespaceLeft (str: string) : string = 
        str
        |> Seq.takeWhile Char.IsWhiteSpace
        |> Seq.toArray
        |> System.String

    let whitespaceRight (str: string) : string = 
        str
        |> Seq.rev
        |> Seq.takeWhile Char.IsWhiteSpace
        |> Seq.rev
        |> Seq.toArray
        |> System.String

    let analyzeWhitespaceLeft (lineNumber: int) (originalLine: string, translatedLine: string): Warning list = [
        let wsO, wsT = 
            whitespaceLeft originalLine, 
            whitespaceLeft translatedLine
        if wsO <> wsT then 
            yield DifferentWhitespaceLeft(lineNumber, (wsO, wsT))
    ]

    let analyzeWhitespaceRight (lineNumber: int) (originalLine: string, translatedLine: string): Warning list = [
        let wsO, wsT = 
            whitespaceRight originalLine, 
            whitespaceRight translatedLine
        if wsO <> wsT then
            yield DifferentWhitespaceRight(lineNumber, (wsO, wsT))
    ]

let analyzeTranslation (original: string, translated: string) : Warning list = [
    match () with
    | _ when original.Trim() = "" -> 
        yield OriginalStringEmpty
    | _ when translated.Trim() = "" -> 
        yield NoTranslation
    | _ ->

    yield! analyzePlaceholders (original, translated)

    let oLines, tLines = original.Split '\n', translated.Split '\n'
    if oLines.Length <> tLines.Length then
        yield DifferentNumberOfLines(oLines.Length, tLines.Length)
    else
        let lines = Array.zip oLines tLines
        for lineNumber in 1..lines.Length do
            let linePair = lines.[lineNumber-1]
            yield! analyzeWhitespaceLeft lineNumber linePair
            yield! analyzeWhitespaceRight lineNumber linePair
]

let analyzeRecord (record: TranslationRecord) : Warning list =
    match record.Translated with
    | TranslatedString.NeedsReview translated 
        -> analyzeTranslation (record.Original, translated)
    | _ -> []
