module TNT.Library.Commands

open Chiron

[<Struct; RQA>]
type When = 
    | BeforeExtract

module private Names =
    let [<Literal>] When = "when"
    let [<Literal>] BeforeExtract = "before-extract"

    let [<Literal>] Command = "command"

module Trigger = 
    let toString = function
        | When.BeforeExtract -> Names.BeforeExtract

    let fromString = function
        | Names.BeforeExtract -> When.BeforeExtract
        | str -> failwithf "Unsupported command trigger '%s'" str

[<Struct>]
type Command = 
    | Command of When * string

let serialize (commands: Command list) = 

    commands
    |> List.map ^ fun (Command(when', cmd)) -> Json.object [
            Names.When, when' |> Trigger.toString |> Json.string
            Names.Command, Json.string cmd
        ]
    |> Seq.toList
    |> Array
    |> Json.formatWith JsonFormattingOptions.Pretty

let deserialize (js: string) = 
    
    let js = Json.parse js
    match js with
    | Array list ->
        let cmd (js: Json) : Command = 
            js 
            |> Json.destructure ^ json {
                let! trigger = Json.read Names.When
                let! command = Json.read Names.Command
                return Command(Trigger.fromString trigger, command)
            }
        list
        |> List.map cmd

    | _ -> failwith "expect a Json array"
