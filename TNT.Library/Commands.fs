module TNT.Library.Commands

open Chiron

[<Struct; RQA>]
type When = 
    | BeforeExtract

module private Names =
    let [<Literal>] When = "when"
    let [<Literal>] BeforeExtract = "before-extract"

    let [<Literal>] Command = "command"

module When = 
    let toString = function
        | When.BeforeExtract -> Names.BeforeExtract

    let tryFromString = function
        | Names.BeforeExtract -> Some When.BeforeExtract
        | _ -> None

    let fromString str = 
        match tryFromString str with
        | None -> failwithf "Unsupported command trigger '%s'" str
        | Some w -> w

[<Struct>]
type Command = 
    | Command of When * string

let serialize (commands: Command list) = 

    commands
    |> List.map ^ fun (Command(when', cmd)) -> Json.object [
            Names.When, when' |> When.toString |> Json.string
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
                return Command(When.fromString trigger, command)
            }
        list
        |> List.map cmd

    | _ -> failwith "expect a Json array"

let filter (now: When) (commands: Command list) : string list =
    commands 
    |> List.choose ^ fun (Command(w, cmd)) -> if w = now then Some cmd else None
