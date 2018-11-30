module TNT.Tests.Analysis

open TNT.Library.Verification.Helper

open FsUnit.Xunit.Typed
open Xunit

[<Theory>]
[<InlineData("{0}{0:C2}{1,15}{0,-28:C2}{1,14:C2}", "{0},{0:C2},{1,15},{0,-28:C2},{1,14:C2}")>]
[<InlineData("{0:00.00}{0:(###) ###-####}{0:[##-##-##]}", "{0:00.00},{0:(###) ###-####},{0:[##-##-##]}")>]
[<InlineData("{0,5:#.###}{0:0.###E+0}", "{0,5:#.###},{0:0.###E+0}")>]
[<InlineData("{0:#,##0,,}", "{0:#,##0,,}")>]
[<InlineData("{0:MM/dd/yy H:mm:ss zzz}", "{0:MM/dd/yy H:mm:ss zzz}")>]
[<InlineData("{{}}", "")>]
[<InlineData("before {0} after {2} end", "{0},{2}")>]
let ``detect and extract common placeholders``(str: string, phs: string) = 
    str
    |> placeholders
    |> String.concat ","
    |> should equal phs


[<Theory>]
[<InlineData("", "")>]
[<InlineData("\r", "\r")>]
[<InlineData("\rx", "\r")>]
[<InlineData("\r  \tx ", "\r  \t")>]
let ``get whitespace left``(str: string, ws: string) = 
    str |> whitespaceLeft |> should equal ws

[<Theory>]
[<InlineData("", "")>]
[<InlineData("\r", "\r")>]
[<InlineData("x\r", "\r")>]
[<InlineData(" x\r  \t", "\r  \t")>]
let ``get whitespace right``(str: string, ws: string) = 
    str |> whitespaceRight |> should equal ws
