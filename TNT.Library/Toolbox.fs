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
            -> RelativePath (Path.Combine(parent, rel))

    let ofString (path: string) = 
        if Path.IsPathRooted(path)
        then AbsolutePath ^ Path.parse path
        else RelativePath path

/// A filename tagged with a phantom type.
type 't filename = 
    | Filename of string
    override this.ToString() = 
        this |> function Filename fn -> fn