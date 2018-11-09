namespace TNT.Library

open System.IO
open FunToolbox.FileSystem

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

[<AutoOpen>] 
/// copied from FunToolbox/FileSystem.
module private FileSystemHelpers =

    let normalize (path: string) = 
        path.Replace("\\", "/")

    let isValidPathCharacter = 
        let invalidPathChars = Path.GetInvalidPathChars()
        fun c ->
            invalidPathChars
            |> Array.contains c
            |> not

    let isEmpty (str: string) = str.Length = 0

    let isTrimmed (str: string) = str.Trim() = str

    let isValidPath (path: string) =
        not ^ isEmpty path
        && isTrimmed path
        && path |> Seq.forall isValidPathCharacter

/// Absolute or relative path. Toolbox candidate.
type ARPath =
    | AbsolutePath of Path
    | RelativePath of string
    override this.ToString() = 
        match this with
        | AbsolutePath p -> string p
        | RelativePath p -> p

/// A tagged, relative path.
type [<Struct>] 'tag rpath = 
    private 
    | RPath of string
    override this.ToString() = 
        this |> function RPath path -> path

/// A filename tagged with a phantom type.
type 'tag filename = 
    | Filename of string
    override this.ToString() = 
        this |> function Filename fn -> fn

module RPath =

    let parse (path: string) = 
        let normalized = normalize path
        if (not ^ isValidPath normalized) then
            failwithf "'%s' is not a valid path" path
        RPath normalized

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
        if parentPath = null then None
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

    let parse (path: string) = 
        if Path.IsPathRooted(path)
        then AbsolutePath ^ Path.parse path
        else RelativePath path

    /// Returns an absolute path be prepending root to it if it's relative.
    let at (root: Path) = function
        | AbsolutePath abs -> abs
        | RelativePath rel -> root |> Path.extend rel

    let extend (right: ARPath) (left: ARPath) =
        match left, right with
        | RelativePath ".", right
            -> right
        | left, RelativePath "."
            -> left
        | _, AbsolutePath path 
            -> AbsolutePath path
        | AbsolutePath path, RelativePath rel 
            -> AbsolutePath (path |> Path.extend rel)
        | RelativePath parent, RelativePath rel 
            -> RelativePath (Path.Combine(parent, rel) |> normalize)

module Path = 

    let inline extend (right: 'tag rpath) (left: Path) : Path = 
        left |> Path.extend (string right)

    let inline extendF (right: 'tag filename) (left: Path) : Path = 
        left |> extend (RPath.ofFilename right)

module Filename = 

    let inline toPath (fn: 'tag filename) : 'tag rpath = 
        RPath.ofFilename fn

    let inline at (path: Path) (fn: 'tag filename) : Path = 
        path |> Path.extend (toPath fn)

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
