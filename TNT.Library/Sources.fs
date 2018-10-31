namespace TNT.Library

open TNT.Model
open System.Json
open Chiron

module Sources =

    let deserialize (json: string) : Sources = 
        let value = JsonValue.Parse json

        let str (value: JsonValue) : string = 
            JsonValue.op_Implicit(value)
    
        let parseAssemblySource (value: JsonValue) : Source = 
            AssemblySource(AssemblyPath(str value.["path"]))

        let parseSource (value: JsonArray) : Source =
            if value.Count <> 2 then
                failwithf "a source must have a type and a content object"
            match str value.[0] with
            | "assembly" -> parseAssemblySource value.[1]
            | unknown -> failwithf "unsupported source type: '%s'" unknown
    

        let language = str value.["language"] |> Language
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

[<AutoOpen>]
module SourceExtensions =

    type Source with
        member this.Format = 
            match this with
            | AssemblySource path -> Format.prop "assembly" (string path)

    type Sources with
        member this.Format = [
            Format.prop "language" (string this.Language)
            Format.group "sources" 
                (this.Sources |> Set.toList |> List.map ^ fun s -> s.Format)
        ]
