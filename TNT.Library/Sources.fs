module TNT.Library.Sources

open TNT.Model
open System.Json
open Chiron

let deserialize (json: string) : Sources = 
    let value = JsonValue.Parse json
    
    let parseAssemblySource (value: JsonValue) : Source = 
        AssemblySource(AssemblyPath(string value.["path"]))

    let parseSource (value: JsonArray) : Source =
        if value.Count <> 2 then
            failwithf "a source must have a type and a content object"
        let sourceType = string value.[0]
        match sourceType with
        | "assembly" -> parseAssemblySource value.[1]
        | unknown -> failwithf "unsupported source type: '%s'" unknown
    
    let language = value.["language"] |> string |> Language
    let sources = 
        value.["sources"] :?> JsonArray
        |> Seq.map (unbox >> parseSource)
        |> Seq.toList
    {
        Language = language
        Sources = sources |> Set.ofList
    }

let serialize (sources: Sources) : string = 

    let obj = Map.ofList >> Object
    let inline str (v: 'v) = v |> string |> String
    let src name properties = Array [String name; obj properties]

    let sourceJson (source: Source) = 
        match source with
        | AssemblySource path -> src "assembly" ["path", str path]

    obj [
        "language", str sources.Language
        "sources", Array (sources.Sources |> Seq.map sourceJson |> Seq.toList)
    ]
    |> Json.formatWith JsonFormattingOptions.Pretty
