/// Global keyboard shortcuts via RegisterHotKey on a message-only window.
module Squarely.Hotkeys

open System.Collections.Generic
open System.Diagnostics
open System.Windows.Forms
open Squarely.Native
open Squarely.Config

let private HWND_MESSAGE = nativeint -3

type HotkeyHost() =
    inherit NativeWindow()

    let handlers = Dictionary<int, unit -> unit>()
    let mutable nextId = 1

    member this.Create() =
        this.CreateHandle(CreateParams(Parent = HWND_MESSAGE))

    /// Returns false if another application already owns the shortcut.
    member this.Register(hk: Hotkey, handler: unit -> unit) =
        let id = nextId
        nextId <- nextId + 1

        if RegisterHotKey(this.Handle, id, hk.Modifiers ||| MOD_NOREPEAT, uint32 (int hk.Key)) then
            handlers[id] <- handler
            true
        else
            false

    member this.Clear() =
        for id in handlers.Keys do
            UnregisterHotKey(this.Handle, id) |> ignore

        handlers.Clear()

    override this.WndProc(m: Message byref) =
        if m.Msg = WM_HOTKEY then
            match handlers.TryGetValue(int m.WParam) with
            | true, handler ->
                try
                    handler ()
                with ex ->
                    Debug.WriteLine ex
            | _ -> ()

        base.WndProc(&m)
