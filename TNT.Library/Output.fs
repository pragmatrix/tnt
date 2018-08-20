/// A computation expressen that supports logging string with the yield keyword.
module TNT.Library.Output

type Output = 
    | E of string
    | W of string
    | I of string
    | D of string

type Writer = Output -> unit
type 'r output = Writer -> 'r

type OutputComputationBuilder() =
    member __.Bind(comp: 'i output, cont: 'i -> 'r output) : 'r output =
        fun writer ->
        let i = comp writer
        cont i writer

    member __.Delay(f) = f()

    member __.YieldFrom (o: Output seq) : unit output = 
        fun writer ->
            o |> Seq.iter writer
            ()

    member __.Yield(o : Output) : unit output =
        fun writer ->
            writer o
            ()

    member this.Combine(a: unit output, b: 'b output) : 'b output = 
        this.Bind(a, fun () -> b)

    member __.ReturnFrom(o) = o
    member __.Return(r) = fun _ -> r

let output = OutputComputationBuilder()

module Output = 
    let run (writer: Output -> unit) (output: 'r output) = output writer



