/// User settings persisted as JSON in %APPDATA%\Squarely\config.json.
module Squarely.Config

open System
open System.Collections.Generic
open System.IO
open System.Text.Json
open System.Windows.Forms
open Squarely.Native
open Squarely.Geometry

type Hotkey = { Modifiers: uint32; Key: Keys }

module Hotkey =
    let parse (text: string) : Hotkey option =
        let parts = text.Split('+', StringSplitOptions.TrimEntries)
        let keyToken = parts[parts.Length - 1]

        let mods =
            parts[.. parts.Length - 2]
            |> Array.fold
                (fun acc m ->
                    match acc, m.ToLowerInvariant() with
                    | Some a, ("ctrl" | "control") -> Some(a ||| MOD_CONTROL)
                    | Some a, ("alt" | "option") -> Some(a ||| MOD_ALT)
                    | Some a, "shift" -> Some(a ||| MOD_SHIFT)
                    | Some a, ("win" | "windows" | "super") -> Some(a ||| MOD_WIN)
                    | _ -> None)
                (Some 0u)

        let key =
            match keyToken.ToLowerInvariant() with
            | "enter" | "return" -> Some Keys.Return
            | "backspace" | "back" -> Some Keys.Back
            | "=" | "plus" | "equals" -> Some Keys.Oemplus
            | "-" | "minus" -> Some Keys.OemMinus
            | t when t.Length = 1 && Char.IsDigit t[0] -> Some(enum<Keys> (int Keys.D0 + int t[0] - int '0'))
            | t when t.Length = 1 && Char.IsLetter t[0] -> Some(enum<Keys> (int (Char.ToUpperInvariant t[0])))
            | t when t.Length > 0 && not (Seq.forall Char.IsDigit t) ->
                match Enum.TryParse<Keys>(keyToken, true) with
                | true, k when (k &&& Keys.Modifiers) = Keys.None -> Some k
                | _ -> None
            | _ -> None

        match mods, key with
        | Some m, Some k when m <> 0u -> Some { Modifiers = m; Key = k }
        | _ -> None

    let format (hk: Hotkey) =
        let has flag = (hk.Modifiers &&& flag) <> 0u

        let mods =
            [ if has MOD_CONTROL then "Ctrl"
              if has MOD_ALT then "Alt"
              if has MOD_SHIFT then "Shift"
              if has MOD_WIN then "Win" ]

        let key =
            match hk.Key with
            | Keys.Return -> "Enter"
            | Keys.Back -> "Backspace"
            | Keys.Oemplus -> "="
            | Keys.OemMinus -> "-"
            | k when k >= Keys.D0 && k <= Keys.D9 -> string (int k - int Keys.D0)
            | k -> string k

        String.Join("+", mods @ [ key ])

type Settings =
    { GapSize: int
      DragToSnap: bool
      CycleSizes: bool
      Shortcuts: Map<WindowAction, string> }

/// Rectangle's defaults, with Ctrl+Alt standing in for Ctrl+Option and Win for Cmd.
let private defaultShortcuts =
    Map
        [ LeftHalf, "Ctrl+Alt+Left"
          RightHalf, "Ctrl+Alt+Right"
          TopHalf, "Ctrl+Alt+Up"
          BottomHalf, "Ctrl+Alt+Down"
          TopLeft, "Ctrl+Alt+U"
          TopRight, "Ctrl+Alt+I"
          BottomLeft, "Ctrl+Alt+J"
          BottomRight, "Ctrl+Alt+K"
          FirstThird, "Ctrl+Alt+D"
          CenterThird, "Ctrl+Alt+F"
          LastThird, "Ctrl+Alt+G"
          FirstTwoThirds, "Ctrl+Alt+E"
          LastTwoThirds, "Ctrl+Alt+T"
          Maximize, "Ctrl+Alt+Enter"
          MaximizeHeight, "Ctrl+Alt+Shift+Up"
          Larger, "Ctrl+Alt+="
          Smaller, "Ctrl+Alt+-"
          Center, "Ctrl+Alt+C"
          Restore, "Ctrl+Alt+Backspace"
          NextDisplay, "Ctrl+Alt+Win+Right"
          PreviousDisplay, "Ctrl+Alt+Win+Left" ]

let defaults () =
    { GapSize = 0
      DragToSnap = true
      CycleSizes = true
      Shortcuts =
        allActions
        |> List.map (fun (_, a) -> a, defaultArg (defaultShortcuts.TryFind a) "")
        |> Map.ofList }

let configPath =
    Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.ApplicationData, "Squarely", "config.json")

let save (settings: Settings) =
    Directory.CreateDirectory(Path.GetDirectoryName configPath) |> ignore
    let shortcuts = Dictionary<string, string>()

    for name, action in allActions do
        shortcuts[name] <- defaultArg (settings.Shortcuts.TryFind action) ""

    let doc =
        {| gapSize = settings.GapSize
           dragToSnap = settings.DragToSnap
           cycleSizes = settings.CycleSizes
           shortcuts = shortcuts |}

    File.WriteAllText(configPath, JsonSerializer.Serialize(doc, JsonSerializerOptions(WriteIndented = true)))

/// Load settings, falling back to defaults for anything missing. Returns warnings to show the user.
let load () : Settings * string list =
    let d = defaults ()

    if not (File.Exists configPath) then
        save d
        d, []
    else
        try
            let options =
                JsonDocumentOptions(CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)

            use doc = JsonDocument.Parse(File.ReadAllText configPath, options)
            let root = doc.RootElement

            let prop (name: string) =
                match root.TryGetProperty(name) with
                | true, v -> Some v
                | _ -> None

            let intProp name fallback =
                match prop name with
                | Some v when v.ValueKind = JsonValueKind.Number -> v.GetInt32()
                | _ -> fallback

            let boolProp name fallback =
                match prop name with
                | Some v when v.ValueKind = JsonValueKind.True -> true
                | Some v when v.ValueKind = JsonValueKind.False -> false
                | _ -> fallback

            let warnings = ResizeArray<string>()
            let mutable shortcuts = d.Shortcuts

            match prop "shortcuts" with
            | Some obj when obj.ValueKind = JsonValueKind.Object ->
                for p in obj.EnumerateObject() do
                    match tryParseAction p.Name with
                    | Some a ->
                        let value = if p.Value.ValueKind = JsonValueKind.String then p.Value.GetString() else ""
                        shortcuts <- shortcuts.Add(a, value)
                    | None -> warnings.Add(sprintf "Unknown action \"%s\" in config" p.Name)
            | _ -> ()

            { GapSize = intProp "gapSize" d.GapSize
              DragToSnap = boolProp "dragToSnap" d.DragToSnap
              CycleSizes = boolProp "cycleSizes" d.CycleSizes
              Shortcuts = shortcuts },
            List.ofSeq warnings
        with ex ->
            d, [ sprintf "Couldn't read config, using defaults: %s" ex.Message ]
