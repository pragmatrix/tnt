/// Helper for Chiron
[<RQA>]
module TNT.Library.Json

open Chiron

/// Destructure a Json object with a typed Json<'a> and fail if destructuring
/// leads to an error.
/// Usage: 
/// jsonObject |> Json.destructure ^ json { }
let inline destructure (f: Json<'a>) (js: Json) = 
    match fst ^ f js with
    | Value v -> v
    | Error err -> failwith err

/// Create a Json Object from a list of string * Json tuples.
let inline object (tuples: (string * Json) seq) = 
    Object ^ Map.ofSeq tuples

/// Create a Json Object from a string.
let inline string (str: string) =
    String str

/// Create a Json object from a sequence.
let inline array (lst: Json seq) =
    lst |> Seq.toList |> Array
