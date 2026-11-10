module Squarely.Tests.HotkeyTests

open System.Windows.Forms
open Xunit
open Squarely.Native
open Squarely.Config

[<Theory>]
[<InlineData("Ctrl+Alt+Left")>]
[<InlineData("Ctrl+Alt+Enter")>]
[<InlineData("Ctrl+Alt+Backspace")>]
[<InlineData("Ctrl+Alt+=")>]
[<InlineData("Ctrl+Alt+-")>]
[<InlineData("Ctrl+Alt+Win+Right")>]
[<InlineData("Ctrl+Shift+7")>]
[<InlineData("Alt+F5")>]
let ``parse and format round-trip`` (text: string) =
    Assert.Equal(text, Hotkey.parse text |> Option.map Hotkey.format |> Option.defaultValue "<none>")

[<Fact>]
let ``parse is case-insensitive and accepts aliases`` () =
    Assert.Equal(Some { Modifiers = MOD_CONTROL ||| MOD_ALT; Key = Keys.U }, Hotkey.parse "control + option + u")
    Assert.Equal(Some { Modifiers = MOD_CONTROL; Key = Keys.Return }, Hotkey.parse "ctrl+return")

[<Theory>]
[<InlineData("")>]
[<InlineData("Left")>]
[<InlineData("Ctrl+Alt+")>]
[<InlineData("Ctrl+Bogus+Left")>]
[<InlineData("Ctrl+Alt+NotAKey")>]
[<InlineData("Ctrl+Alt+42")>]
let ``invalid shortcuts are rejected`` (text: string) =
    Assert.Equal(None, Hotkey.parse text)
