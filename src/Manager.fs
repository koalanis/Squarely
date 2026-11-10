/// Executes actions against windows and remembers enough history for Restore and size cycling.
module Squarely.Manager

open System.Collections.Generic
open System.Drawing
open System.Windows.Forms
open Squarely.Native
open Squarely.Geometry
open Squarely.Config
open Squarely.WindowOps

type private Record =
    { /// Frame before Squarely first touched the window; what Restore returns to.
      Original: Rect
      Action: WindowAction
      /// Frame the window actually ended up with (apps may enforce minimum sizes).
      Applied: Rect
      Cycle: int }

let private history = Dictionary<nativeint, Record>()

let private prune () =
    for hwnd in List.ofSeq history.Keys do
        if not (IsWindow hwnd) then
            history.Remove hwnd |> ignore

let private orderedScreens () =
    Screen.AllScreens |> Array.sortBy (fun s -> s.Bounds.X, s.Bounds.Y)

let private commit hwnd original action cycle target =
    setFrame hwnd target

    history[hwnd] <-
        { Original = original
          Action = action
          Applied = getFrame hwnd
          Cycle = cycle }

/// `reference` is the frame used to decide whether the window still sits where we last put it;
/// if it does, this is a repeat (cycle sizes, keep the original restore frame).
let private apply (settings: Settings) hwnd action (screen: Screen) (reference: Rect) allowCycle =
    prune ()
    let frame = getFrame hwnd
    let work = toRect screen.WorkingArea
    let gap = max 0 settings.GapSize

    let inPlace =
        match history.TryGetValue hwnd with
        | true, r when approxEqual 2 r.Applied reference -> Some r
        | _ -> None

    let original =
        inPlace |> Option.map (fun r -> r.Original) |> Option.defaultValue reference

    match action with
    | Restore ->
        match history.TryGetValue hwnd with
        | true, r ->
            setFrame hwnd r.Original
            history.Remove hwnd |> ignore
        | _ -> ()

    | NextDisplay
    | PreviousDisplay ->
        let screens = orderedScreens ()

        if screens.Length > 1 then
            let i =
                screens
                |> Array.tryFindIndex (fun s -> s.DeviceName = screen.DeviceName)
                |> Option.defaultValue 0

            let step = if action = NextDisplay then 1 else -1
            let dest = screens[(i + step + screens.Length) % screens.Length]
            let destWork = toRect dest.WorkingArea

            // A tiled window keeps its tiling on the new display; anything else moves proportionally.
            let retiled =
                inPlace
                |> Option.bind (fun r -> tile r.Action r.Cycle gap destWork |> Option.map (fun t -> r, t))

            match retiled with
            | Some(r, t) -> commit hwnd original r.Action r.Cycle t
            | None ->
                let last = inPlace |> Option.map (fun r -> r.Action) |> Option.defaultValue action
                commit hwnd original last 0 (translate work destWork frame)

    | _ ->
        let cycle =
            match inPlace with
            | Some r when allowCycle && settings.CycleSizes && r.Action = action -> r.Cycle + 1
            | _ -> 0

        match tile action cycle gap work with
        | Some t -> commit hwnd original action cycle t
        | None -> reposition action work frame |> Option.iter (commit hwnd original action 0)

/// Run an action from a keyboard shortcut or the tray menu.
let run settings hwnd action =
    if isManageable hwnd then
        let frame = getFrame hwnd
        apply settings hwnd action (Screen.FromRectangle(toRectangle frame)) frame true

/// Snap a window that was dragged into a snap area.
let snapFromDrag settings hwnd action screen (dragStart: Rect) =
    if isManageable hwnd then
        apply settings hwnd action screen dragStart false

/// Dragging a snapped window out of its spot restores its pre-snap size, like Rectangle.
let unsnapAfterDrag hwnd (dragStart: Rect) (cursor: Point) =
    match history.TryGetValue hwnd with
    | true, r ->
        history.Remove hwnd |> ignore

        let sizeChanged = r.Applied.W <> r.Original.W || r.Applied.H <> r.Original.H

        if approxEqual 2 r.Applied dragStart && sizeChanged then
            let frame = getFrame hwnd
            let work = toRect (Screen.FromPoint cursor).WorkingArea
            // Keep the cursor at the same relative spot along the title bar.
            let grab = float (cursor.X - frame.X) / float (max 1 frame.W)
            let x = cursor.X - int (grab * float r.Original.W)

            setFrame
                hwnd
                (fit
                    work
                    { X = x
                      Y = frame.Y
                      W = r.Original.W
                      H = r.Original.H })
    | _ -> ()
