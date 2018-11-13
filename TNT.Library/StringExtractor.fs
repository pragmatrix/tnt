/// With Mono.Cecil we extract strings from assemblies that are 
/// marked with functions in TNT.CSharp or TNT.FSharp.
module TNT.Library.StringExtractor

open FunToolbox.FileSystem
open Mono.Cecil
open Mono.Cecil.Cil
open TNT.Model

[<Struct>]
type ExtractionError = 
    | NoStringBeforeInvocationOfT
    override this.ToString() =
        match this with
        | NoStringBeforeInvocationOfT -> 
            "Failed to extract the string before .t(), please be sure that the string is a literal"

[<Struct>]
type PhysicalContext = 
    | PhysicalContext of methodName: string
    override this.ToString() = 
        this |> function PhysicalContext methodName -> methodName

[<Struct>]
type ExtractionContext = {
    Logical: LogicalContext
    Physical: PhysicalContext option
}

type ExtractionErrors = (ExtractionError * ExtractionContext) list

module ExtractionErrors = 

    let format (errors: ExtractionErrors) = 
        errors
        |> List.map ^ fun (e, context) ->
            Format.group (string context.Physical) [
                Format.prop "error" (string e)
                Format.prop "context" (string context.Logical)
            ]
    
[<AutoOpen>]
module internal Helper = 

    let TFunctions = [|
        "System.String TNT.Extensions::t(System.String)"
        "System.String TNT.FSharp.Extensions::String.get_t(System.String)"
    |]

    let extractFromInstructions (instructions : Instruction seq) 
        : Result<string, ExtractionError> seq = 

        let isTranslationCall (instruction: Instruction) = 
            match instruction.OpCode with
            | op when op = OpCodes.Call -> 
                match instruction.Operand with
                | :? MethodReference as md ->
                    TFunctions |> Array.contains md.FullName
                | _ -> false
            | _ -> false

        instructions
        |> Seq.rev
        |> Seq.choose ^ fun instruction -> 
            let prevInst = instruction.Previous
            if isTranslationCall instruction && prevInst <> null then
                match prevInst.OpCode with
                | op when op = OpCodes.Ldstr 
                    -> Some ^ Ok ^ string prevInst.Operand
                | _ -> Some ^ Error NoStringBeforeInvocationOfT
            else None

    let logicalContext (typeDefinition: TypeDefinition) : LogicalContext =

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

    let physicalContext (methodDefinition: MethodDefinition) : PhysicalContext option =
        let body = methodDefinition.Body
        if  methodDefinition.DebugInformation <> null
            && methodDefinition.DebugInformation.HasSequencePoints
            && body.Instructions.Count > 0 
            && body.Instructions.[0] <> null then
            let sp = methodDefinition.DebugInformation.GetSequencePoint(body.Instructions.[0])
            if sp <> null && sp.Document <> null then
                Some ^ PhysicalContext sp.Document.Url
            else 
                None
        else None

    let errorContext (methodDefinition: MethodDefinition) : ExtractionContext = {
        Logical = logicalContext methodDefinition.DeclaringType
        Physical = physicalContext methodDefinition
    }

let extract (path: Path) : OriginalStrings * ExtractionErrors = 

    let assemblyDefinition = 
        AssemblyDefinition.ReadAssembly(
            string path, ReaderParameters(ReadSymbols = true))

    let rec extractFromType 
        (typeDefinition: TypeDefinition) 
        : (Result<string, ExtractionError> * ExtractionContext) seq = seq {
        yield!
            typeDefinition.NestedTypes
            |> Seq.collect ^ fun nestedTypeDefinition ->
                extractFromType nestedTypeDefinition
        yield!
            typeDefinition.Methods
            |> Seq.collect ^ fun methodDefinition ->
                let body = methodDefinition.Body
                // body may be null for abstract / interface methods.
                if body = null then Seq.empty else
                let errorContext = errorContext methodDefinition
                body.Instructions 
                |> extractFromInstructions
                |> Seq.map ^ fun result -> result, errorContext
    }

    let stringsAndErrors = 
        assemblyDefinition.Modules
        |> Seq.collect ^ fun moduleDefinition ->
            moduleDefinition.Types
            |> Seq.collect extractFromType
            |> Seq.toList
        |> Seq.toList

    let strings =
        stringsAndErrors
        |> List.choose ^ fun (r, context) ->
                match r with
                | Ok(str) -> Some (str, List.singleton context.Logical)
                | Error _ -> None


    let errors =
        stringsAndErrors
        |> List.choose ^ fun (r, context) ->
                match r with
                | Error(err) -> Some (err, context)
                | Ok _ -> None

    strings |> OriginalStrings.create,
    errors

    

    

    
    
