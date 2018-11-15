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
            let title = 
                match context.Physical with
                | Some physical -> string physical
                | None -> "[no location available, please build your project with debug symbols enabled]"

            Format.group title [
                Format.prop "error" (string e)
                Format.prop "context" (string context.Logical)
            ]
    
[<AutoOpen>]
module internal Helper = 

    let TFunctions = [|
        "System.String TNT.Extensions::t(System.String)", 1
        "System.String TNT.Extensions::t(System.String,System.String)", 2
        "System.String TNT.FSharp.Extensions::String.get_t(System.String)", 1
    |]

    // https://github.com/jbevain/cecil/blob/eea822cad4b6f320c9e1da642fcbc0c129b00a6e/Mono.Cecil.Cil/CodeWriter.cs#L437

    type IMethodSignature with
        member this.HasImplicitThis() = this.HasThis && not this.ExplicitThis

    let tryComputeStackDelta (instruction: Instruction) : int option =
        match instruction.OpCode.FlowControl with
        | FlowControl.Call ->
            let method = instruction.Operand :?> IMethodSignature;
            Seq.sum ^ seq {
                // pop 'this' argument
                if method.HasImplicitThis() && instruction.OpCode.Code <> Code.Newobj then
                    yield -1
                // pop normal arguments
                if method.HasParameters then
                    yield -method.Parameters.Count;
                // pop function pointer
                if instruction.OpCode.Code = Code.Calli then
                    yield -1
                // push return value
                if method.ReturnType.MetadataType <> MetadataType.Void || instruction.OpCode.Code = Code.Newobj then
                    yield 1
            }
            |> Some
        | _ -> 
        
        let popDelta = 
            match instruction.OpCode.StackBehaviourPop with
            | StackBehaviour.Popi
            | StackBehaviour.Popref
            | StackBehaviour.Pop1 -> Some -1
            | StackBehaviour.Pop1_pop1
            | StackBehaviour.Popi_pop1
            | StackBehaviour.Popi_popi
            | StackBehaviour.Popi_popi8
            | StackBehaviour.Popi_popr4
            | StackBehaviour.Popi_popr8
            | StackBehaviour.Popref_pop1
            | StackBehaviour.Popref_popi -> Some -2
            | StackBehaviour.Popi_popi_popi
            | StackBehaviour.Popref_popi_popi
            | StackBehaviour.Popref_popi_popi8
            | StackBehaviour.Popref_popi_popr4
            | StackBehaviour.Popref_popi_popr8
            | StackBehaviour.Popref_popi_popref -> Some -3
            | StackBehaviour.PopAll -> None
            | _ -> Some 0
        
        match popDelta with
        | None -> None
        | Some popDelta ->

        let pushDelta = 
            match instruction.OpCode.StackBehaviourPush with
            | StackBehaviour.Push1
            | StackBehaviour.Pushi
            | StackBehaviour.Pushi8
            | StackBehaviour.Pushr4
            | StackBehaviour.Pushr8
            | StackBehaviour.Pushref -> Some 1
            | StackBehaviour.Push1_push1 -> Some 2
            | _ -> None

        pushDelta |> Option.map ((+) popDelta)

    /// Try to find the instruction that pushes the argument number (one based)
    /// on the stack.
    let tryLocateArgumentPushInstruction (arg: int) (callInstruction: Instruction) : Instruction option =
        
        if callInstruction.Previous = null 
        then None else

        let rec find (instruction: Instruction) (delta: int) = 
            match tryComputeStackDelta instruction with
            | Some d when delta + d = arg 
                -> Some instruction
            | Some d when instruction.Previous <> null 
                -> find (instruction.Previous) (delta + d)
            | _ -> None

        find callInstruction.Previous 0

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
                    TFunctions
                    |> Array.tryFind (fst >> (=) md.FullName) 
                    |> Option.map ^ T2.mapFst ^ fun _ -> instruction
                | _ -> None
            | _ -> None

        instructions
        |> Seq.choose ^ isTranslationCall
        |> Seq.map ^ fun (callInstruction, arg) -> 
            let physicalLocation = resolvePhysicalLocation methodDefinition callInstruction
            match tryLocateArgumentPushInstruction arg callInstruction with
            | Some inst when inst.OpCode = OpCodes.Ldstr 
                -> Ok ^ string inst.Operand, physicalLocation
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

    

    

    
    
