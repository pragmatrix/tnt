/// With Mono.Cecil we extract strings from assemblies that are 
/// marked with functions in TNT.CSharp or TNT.FSharp.
module TNT.Library.StringExtractor

let extract (name: AssemblyName) (assembly: byte[]) : string list = 
    []
