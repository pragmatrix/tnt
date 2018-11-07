/// With Mono.Cecil we extract strings from assemblies that are 
/// marked with functions in TNT.CSharp or TNT.FSharp.
module TNT.Library.StringExtractor

open FunToolbox.FileSystem
open Mono.Cecil
open Mono.Cecil.Cil
open TNT.Model

[<AutoOpen>]
module private Private = 

    let TFunctions = [|
        "System.String TNT.Extensions::t(System.String)"
        "System.String TNT.FSharp.Extensions::String.get_t(System.String)"
    |]

    let extractFromInstructions (instructions : Instruction seq) : string seq = 

        let isTranslationCall (instruction: Instruction) = 
            match instruction.OpCode with
            | op when op = OpCodes.Call -> 
                match instruction.Operand with
                | :? MethodReference as md ->
                    TFunctions |> Array.contains md.FullName
                | _ -> false
            | _ -> false

        instructions
        |> Seq.choose ^ fun instruction -> 
            match instruction.OpCode with
            | op when op = OpCodes.Ldstr 
                && instruction.Next <> null 
                && isTranslationCall instruction.Next
                -> Some ^ string instruction.Operand
            | _ -> None

let private logicalContext (typeDefinition: TypeDefinition) : LogicalContext =

    let rec buildContext (typeDefinition: TypeDefinition) (soFar: string list) = 

        let isCompilerGenerated = 
            typeDefinition.HasCustomAttributes 
            && (typeDefinition.CustomAttributes 
                |> Seq.exists ^ fun attr -> attr.AttributeType.Name = "CompilerGeneratedAttribute")
        
        let soFar = 
            if isCompilerGenerated
            then soFar
            else typeDefinition.Name :: soFar
            
        match typeDefinition.DeclaringType with
        | null -> typeDefinition.Namespace :: soFar
        | dt -> buildContext dt soFar

    buildContext typeDefinition []
    |> String.concat "."
    |> LogicalContext

let extract (path: Path) : OriginalStrings = 

    let assemblyDefinition = AssemblyDefinition.ReadAssembly(string path)

    let rec extractFromType (typeDefinition: TypeDefinition) : (string * LogicalContext) seq = seq {
        yield!
            typeDefinition.NestedTypes
            |> Seq.collect ^ fun nestedTypeDefinition ->
                extractFromType nestedTypeDefinition
        yield!
            typeDefinition.Methods
            |> Seq.collect ^ fun methodDefinition ->
                let body = methodDefinition.Body
                // body may be null for abstract / interface methods.
                if body <> null then
                    let context = logicalContext typeDefinition
                    body.Instructions 
                    |> extractFromInstructions
                    |> Seq.map ^ fun str -> str, context
                else
                    Seq.empty
    }

    assemblyDefinition.Modules
    |> Seq.collect ^ fun moduleDefinition ->
        moduleDefinition.Types
        |> Seq.collect extractFromType
    |> Seq.mapSnd List.singleton
    |> OriginalStrings.create

    

    

    
    
