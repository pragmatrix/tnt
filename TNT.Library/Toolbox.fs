namespace TNT.Library

open System.IO
open FunToolbox.FileSystem
open System.Diagnostics

[<AutoOpen>]
module Prelude =

    /// Trimmed String conversion.
    let (|Trimmed|) (str: string) =
        str.Trim()

    /// Match if the string has content after trimmed, and return the trimmed string.
    let (|NotEmptyTrimmed|_|) (Trimmed trimmed) = 
        match trimmed with
        | "" -> None
        | str -> Some str

module List = 
    /// Return the list, and all the sub-lists possible, except the empty list.
    /// [a;b] -> [ [a;b]; [b] ]
    /// [a;b;c] -> [ [a;b;c]; [b;c]; [c]]
    /// [] -> []
    let subs l = 
        
        let rec subs soFar todo =
            match todo with
            | [] -> soFar |> List.rev
            | _::rest -> subs (todo :: soFar) rest

        l |> subs []

    /// Matches if all the element's sequence are equal or no elements 
    /// are in the sequence.
    /// Returns the element if they are.
    let inline (|AllEqual|_|) l = 
        match l with
        | [] -> Some None
        | [one] -> Some ^ Some one
        | head::rest -> 
            if rest |> List.forall ^ (=) head 
            then Some ^ Some head
            else None

module Result = 

    let toOption = function
        | Ok r -> Some r
        | Error _ -> None

[<AutoOpen>] 
/// copied from FunToolbox/FileSystem.
module private FileSystemHelpers =

    let isEmpty (str: string) = str.Length = 0

    let isTrimmed (str: string) = str.Trim() = str

    let normalize (path: string) = 
        path.Replace("\\", "/")

    let isValidPathCharacter = 
        let invalidPathChars = Path.GetInvalidPathChars()
        fun c ->
            invalidPathChars
            |> Array.contains c
            |> not

    let (|ValidPath|_|) (path: string) =
        if not ^ isEmpty path
            && isTrimmed path
            && path |> Seq.forall isValidPathCharacter 
            then Some path else None

    let isValidNameCharacter =
        let invalidNameChars = Path.GetInvalidFileNameChars()
        fun c ->
            invalidNameChars
            |> Array.contains c
            |> not

    let (|ValidFilename|_|) (name: string) =
        if not ^ isEmpty name
            && isTrimmed name
            && name |> Seq.forall isValidNameCharacter 
            then Some name else None

/// A tagged, relative path.
type [<Struct>] 'tag rpath = 
    private 
    | RPath of string
    override this.ToString() = 
        this |> function RPath path -> path

/// Absolute or relative path. Toolbox candidate.
type 'tag arpath =
    private
    | AbsolutePath of Path
    | RelativePath of 'tag rpath
    override this.ToString() = 
        match this with
        | AbsolutePath p -> string p
        | RelativePath p -> string p

/// A filename tagged with a phantom type.
type 'tag filename = 
    | Filename of string
    override this.ToString() = 
        this |> function Filename fn -> fn


module RPath =

    let parse (path: string) = 
        let normalized = normalize path
        match normalized with
        | ValidPath path -> RPath path
        | _ -> failwithf "'%s' is not a valid path" path

    let map (f: string -> string) (Path path) =
        path |> f |> parse

    let extend (right: 'tag rpath) (left: _ rpath) : 'tag rpath =
        Path.Combine(string left, string right)
        |> parse

    let inline ofFilename (fn: 'tag filename): 'tag rpath =
        string fn |> parse

    let inline extendF (right: 'tag filename) (path: _ rpath) : 'tag rpath = 
        path |> extend (ofFilename right)

    let inline at (absolute: Path) (path: _ rpath) : Path =
        absolute |> Path.extend (string path)

    let parent (path: 'tag rpath) : _ rpath option = 
        let parentPath = Path.GetDirectoryName (string path)
        // null: root path, empty: no directory information.
        if parentPath = null || parentPath = "" then None
        else Some ^ parse parentPath

    let name (path: 'tag rpath) : string =
        Path.GetFileName(string path)

    let parts (path: 'tag rpath) : string list =
        
        let rec getParts (todo: 'tag rpath) (parts: string list) = 
            let parts = name todo :: parts
            match parent todo with
            | Some parent -> getParts parent parts
            | None -> parts

        getParts path []

    let ofParts (parts: string list) : 'tag rpath =
        Path.Combine(List.toArray parts)
        |> parse

module ARPath =

    let tryParse (path: string) = 
        let path = path |> normalize
        match path with
        | ValidPath path ->
            Some ^ 
                if Path.IsPathRooted(path)
                then AbsolutePath ^ Path.parse path
                else RelativePath ^ RPath.parse path
        | _ -> None

    let parse (path: string) = 
        match tryParse path with
        | Some path -> path
        | None -> failwithf "'%s' is an invalid absolute or relative path" path

    /// Returns an absolute path be prepending root to it if it's relative.
    let at (root: Path) = function
        | AbsolutePath abs -> abs
        | RelativePath (RPath rel) -> root |> Path.extend rel

    let extend (right: 'tag arpath) (left: _ arpath) : 'tag arpath =
        match left, right with
        | RelativePath (RPath "."), right
            -> right
        | left, RelativePath (RPath ".")
            -> left
        | _, AbsolutePath path 
            -> AbsolutePath path
        | AbsolutePath path, RelativePath rel 
            -> AbsolutePath (path |> Path.extend (string rel))
        | RelativePath (RPath parent), RelativePath (RPath rel)
            -> RelativePath ^ RPath.parse ^ normalize ^ Path.Combine(parent, rel)

module Path = 

    let inline extend (right: 'tag rpath) (left: Path) : Path = 
        left |> Path.extend (string right)

    let inline extendF (right: 'tag filename) (left: Path) : Path = 
        left |> extend (RPath.ofFilename right)

module Filename = 

    let tryParse = function
        | ValidFilename fn -> Ok ^ Filename fn
        | fn -> Error ^ sprintf "'%s' is not a valid filename" fn

    let parse str = 
        match tryParse str with
        | Ok fn -> fn
        | Error e -> failwith e

    let inline map f (Filename fn) = Filename ^ f fn

    let inline toPath (fn: 'tag filename) : 'tag rpath = 
        RPath.ofFilename fn

    let inline at (path: Path) (fn: 'tag filename) : Path = 
        path |> Path.extend (toPath fn)

    let inline ofARPath (path: 'tag arpath) : 'tag filename =
        Path.GetFileName(string path)
        |> parse

type 'a selector = 
    | SelectAll
    | Select of 'a list

module Selector = 

    let isSelected (item: 'a) = function
        | SelectAll -> true
        | Select specific -> List.contains item specific

    /// Selects from an existing list of items. 
    /// Splits the list of existing items in a selected and the not-seleted items.
    let select (existing: 'a list) (selector: 'a selector) = 
        existing 
        |> List.partition ^ fun item -> isSelected item selector

module ExternalCommand = 
    
    let run command = 
        let si = 
            new ProcessStartInfo (
                command,
                // RedirectStandardInput = true,
                // RedirectStandardOutput = true,
                // RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            )

        use proc = new Process(StartInfo = si)
        if (not ^ proc.Start()) then
            failwithf "failed to run command '%s'" command

        (*
        let outputReceived _ (args : DataReceivedEventArgs) =
            Console.WriteLine(args.Data)

        let errorReceived _ (args: DataReceivedEventArgs) = 
            Console.Error.WriteLine(args.Data)
        
        proc.OutputDataReceived.AddHandler (DataReceivedEventHandler(outputReceived))
        proc.ErrorDataReceived.AddHandler (DataReceivedEventHandler(errorReceived))

        proc.BeginOutputReadLine()
        proc.BeginErrorReadLine()
        *)

        proc.WaitForExit()
        proc.ExitCode

