/// With Mono.Cecil we extract strings from assemblies that are 
/// marked with functions in TNT.CSharp or TNT.FSharp.
module TNT.Library.StringExtractor

open Mono.Cecil
open Mono.Cecil.Cil
open TNT.Model

[<AutoOpen>]
module private Private = 

    let TFunctions = [|
        "System.String TNT.CSharp.Extensions::t(System.String)"
        "System.String TNT.FSharp.Extensions::String.get_t(System.String)"
    |]

    let extractFromInstructions (instructions : Instruction seq) = 

        let isTranslationCall (instruction: Instruction) = 
            match instruction.OpCode with
            | op when op = OpCodes.Call -> 
                match instruction.Operand with
                | :? MethodReference as md ->
                    TFunctions |> Array.contains md.FullName
                | _ -> false
            | _ -> false
            

        instructions
        |> Seq.map ^ fun instruction -> 
            match instruction.OpCode with
            | op when op = OpCodes.Ldstr 
                && instruction.Next <> null 
                && isTranslationCall instruction.Next
                -> Some ^ string instruction.Operand
            | _ -> None
        |> Seq.choose id

let extract (name: AssemblyPath) : OriginalString list = 

    let assemblyDefinition = AssemblyDefinition.ReadAssembly(string name)

    let rec extractFromType (typeDefinition: TypeDefinition) : string seq = seq {
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
                    body.Instructions |> extractFromInstructions
                else
                    Seq.empty
    }

    assemblyDefinition.Modules
    |> Seq.collect ^ fun moduleDefinition ->
        moduleDefinition.Types
        |> Seq.collect extractFromType
    // ensure reproducibility and remove duplicates
    |> Seq.sort |> Seq.distinct
    |> Seq.map OriginalString
    |> Seq.toList

    

    

    
    
