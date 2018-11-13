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
type PhysicalLocation = 
    | PhysicalLocation of filename: string * lineColumn: (int * int)
    override this.ToString() = 
        this 
        |> function 
            PhysicalLocation(filename, (line, column)) 
            -> sprintf "%s(%d,%d)" filename line column
            
[<Struct>]
type ExtractionPoint = {
    Logical: LogicalContext
    Physical: PhysicalLocation option
}

type ExtractionErrors = (ExtractionError * ExtractionPoint) list

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


    let resolvePhysicalLocation 
        (methodDefinition: MethodDefinition) 
        (instruction: Instruction) : PhysicalLocation option = 

        match methodDefinition.DebugInformation with
        | null -> None
        | debugInfo when debugInfo.HasSequencePoints ->
            let instructionOffset = instruction.Offset
            debugInfo.SequencePoints 
            |> Seq.tryFindBack ^ fun sp -> 
                sp.Offset <= instructionOffset
            |> Option.map ^ fun sp -> 
                PhysicalLocation(sp.Document.Url, (sp.StartLine, sp.StartColumn))
        | _ -> None

    let extractFromInstructions 
        (methodDefinition: MethodDefinition)
        (instructions : Instruction seq) 
        : (Result<string, ExtractionError> * PhysicalLocation option) seq = 

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
        |> Seq.filter ^ fun inst -> inst.Previous <> null && isTranslationCall inst
        |> Seq.map ^ fun instruction -> 
            let prevInst = instruction.Previous
            let physicalLocation = resolvePhysicalLocation methodDefinition instruction
            match prevInst.OpCode with
            | op when op = OpCodes.Ldstr 
                -> Ok ^ string prevInst.Operand, physicalLocation
            | _ -> Error NoStringBeforeInvocationOfT, physicalLocation

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

let extract (path: Path) : OriginalStrings * ExtractionErrors = 

    let assemblyDefinition = 
        AssemblyDefinition.ReadAssembly(string path, 
            ReaderParameters(
                // don't throw an exception, when symbols can not be resolved.
                SymbolReaderProvider = DefaultSymbolReaderProvider(false)
            ))

    let rec extractFromType 
        (typeDefinition: TypeDefinition) 
        : (Result<string, ExtractionError> * ExtractionPoint) seq = seq {
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
                let logicalContext = logicalContext typeDefinition
                body.Instructions 
                |> extractFromInstructions methodDefinition
                |> Seq.map ^ fun (result, location) -> result, {
                    Logical = logicalContext
                    Physical = location
                }
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

    

    

    
    
