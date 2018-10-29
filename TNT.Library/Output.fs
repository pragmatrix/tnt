/// A computation expressen that supports logging string with the yield keyword.
module TNT.Library.Output

open System

type Output = 
    | E of string
    | W of string
    | I of string
    | D of string
    override this.ToString() = 
        match this with
        | E s -> "[E] " + s
        | W s -> "[W] " + s
        | I s -> "[I] " + s
        | D s -> "[D] " + s

type Writer = Output -> unit
type 'r output = Writer -> 'r

type OutputComputationBuilder() =

    member inline __.Bind(comp: 'i output, cont: 'i -> 'r output) : 'r output =
        fun writer ->
            let i = comp writer
            cont i writer

    member inline __.YieldFrom (o: Output seq) : unit output = 
        fun writer ->
            o |> Seq.iter writer

    member inline __.Yield(o : Output) : unit output =
        fun writer ->
            writer o

    member inline __.ReturnFrom(o) : _ output = o
    member inline __.Return(r) : _ output = fun _ -> r

    member inline this.Zero() = 
        this.Return()

    member inline this.Delay(f: unit -> 'r output) =
        this.Bind(this.Return(), f)

    member this.Using<'r, 't when 't :> IDisposable>(disposable: 't, body : 't -> 'r output) : 'r output =
         this.TryFinally(this.Delay(fun () -> body disposable), disposable.Dispose)

    member this.While(predicate, body: unit -> unit output) : unit output =
        if predicate()
        then this.Bind(body(), fun () -> this.While(predicate, body))
        else this.Zero()

    member this.For(sequence: _ seq, body) =
        this.Using(sequence.GetEnumerator(), fun enum ->
            this.While(enum.MoveNext, fun () -> body enum.Current))

    member __.TryFinally(body : 'r output, compensation: unit -> unit) : 'r output =
        fun writer ->
            try body writer
            finally compensation() 

    member __.TryWith(body: 'r output, handler: exn -> 'r output) : 'r output =
        fun writer ->
            try body writer
            with e -> handler e writer

    member this.Combine(a, b) = 
        this.Bind(a, fun () -> b)

let output = OutputComputationBuilder()

module Output = 
    let run (writer: Output -> unit) (output: 'r output) : 'r = output writer

    let sequence (output: 'r output list) : 'r list output =
        fun writer ->
            output 
            |> List.map (run writer)

    let map (f: 'a -> 'b) (input: 'a output) : 'b output =
        fun writer ->
            input |> run writer |> f

    let bind (f: 'a -> 'b output) (input: 'a output) : 'b output = 
        output.Bind(input, f)


