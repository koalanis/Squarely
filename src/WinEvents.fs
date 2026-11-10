/// System-wide window event notifications (foreground changes, drag start/end).
module Squarely.WinEvents

open System.Diagnostics
open Squarely.Native

// Delegates handed to native code must stay alive for as long as the hook does.
let private procs = ResizeArray<WinEventProc>()
let private hooks = ResizeArray<nativeint>()

let subscribe (eventMin: uint32) (eventMax: uint32) (handler: uint32 -> nativeint -> unit) =
    let proc =
        WinEventProc(fun _ evt hwnd idObject idChild _ _ ->
            if hwnd <> 0n && idObject = OBJID_WINDOW && idChild = CHILDID_SELF then
                try
                    handler evt hwnd
                with ex ->
                    Debug.WriteLine ex)

    procs.Add proc

    hooks.Add(
        SetWinEventHook(eventMin, eventMax, 0n, proc, 0u, 0u, WINEVENT_OUTOFCONTEXT ||| WINEVENT_SKIPOWNPROCESS)
    )

let unsubscribeAll () =
    for h in hooks do
        UnhookWinEvent h |> ignore

    hooks.Clear()
    procs.Clear()
