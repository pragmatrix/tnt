﻿namespace TNT.Library

[<Struct>]
type IndentedString = 
    | IndentedString of level:int * string

type Property = 
    | Property of string * string
    | Group of string * Property list

/// Some helpers to format structured output.
module Format = 

    let prop name value = 
        Property(name, value)

    let group name list = 
        Group(name, list)

module IndentedString =

    let indent (IndentedString(level, str)) = IndentedString(level + 1, str)

    let string (indent: string) (IndentedString(level, str)) =
        String.replicate level indent + str

module IndentedStrings = 

    let indent (strings: IndentedString list) =
        strings
        |> List.map ^ IndentedString.indent

    let strings (indent: string) (strings: IndentedString list) =
        strings
        |> List.map ^ IndentedString.string indent

module Property =

    let rec indentedStrings = function
        | Property(name, value) -> 
            [ IndentedString(0, name + ": " + value) ]
        | Group(name, list) -> [
            yield IndentedString(0, name + ":")
            for property in list do
                yield!
                    indentedStrings property
                    |> IndentedStrings.indent
        ]

    let strings indent property = 
        property
        |> indentedStrings
        |> IndentedStrings.strings indent

module Properties =

    let indentedStrings properties = 
        properties
        |> List.collect Property.indentedStrings

    let strings indent properties = 
        properties
        |> List.collect ^ Property.strings indent
