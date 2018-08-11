namespace TNT.FSharp

open System.Runtime.CompilerServices

module Extensions = 

    type System.String with

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        member this.t = 
            this


    type [<Measure>] tx

[<assembly:AutoOpen("TNT.FSharp.Extensions")>]
do ()
