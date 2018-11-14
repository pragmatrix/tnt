namespace TNT.WebServer

open Microsoft.AspNetCore.Server.Kestrel

module Say =
    let hello name =
        printfn "Hello %s" name
