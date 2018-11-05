namespace TNT.Library

open System.IO
open FunToolbox.FileSystem

/// Absolute or relative path. Toolbox candidate.
type ARPath =
    | AbsolutePath of Path
    | RelativePath of string
    override this.ToString() = 
        match this with
        | AbsolutePath p -> string p
        | RelativePath p -> p

module ARPath =

    let private normalize (path: string) = 
        path.Replace("\\", "/")

    /// Returns an absolute path be prepending root to it if it's relative.
    let rooted (root: Path) = function
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

    let ofString (path: string) = 
        if Path.IsPathRooted(path)
        then AbsolutePath ^ Path.parse path
        else RelativePath path

/// A tagged, relative path.
type [<Struct>] 'tag rpath = 
    | RPath of string
    override this.ToString() = 
        this |> function RPath path -> path

/// A filename tagged with a phantom type.
type 'tag filename = 
    | Filename of string
    override this.ToString() = 
        this |> function Filename fn -> fn

module RPath =

    let extend (right: 'tag rpath) (left: _ rpath) : 'tag rpath =
        let left, right = 
            RelativePath (string left),
            RelativePath (string right)
        left 
        |> ARPath.extend right
        |> string
        |> RPath

    let inline ofFilename (fn: 'tag filename): 'tag rpath =
        string fn |> RPath

    let inline extendF (right: 'tag filename) (path: _ rpath) : 'tag rpath = 
        path |> extend (ofFilename right)

    let inline at (absolute: Path) (path: _ rpath) : Path =
        absolute |> Path.extend (string path)

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
