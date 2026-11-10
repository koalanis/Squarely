/// Drag-to-snap: while a window is being dragged, preview and apply a snap area at the screen edges.
module Squarely.DragSnap

open System.Windows.Forms
open Squarely.Geometry
open Squarely.Config
open Squarely.WindowOps
open Squarely.Overlay

type DragSnapper(overlay: Overlay, getSettings: unit -> Settings) =
    let timer = new Timer(Interval = 15)
    let mutable target = 0n
    let mutable start = Unchecked.defaultof<Rect>
    let mutable moved = false
    let mutable zone: (WindowAction * Screen) option = None

    let update () =
        if target <> 0n then
            let frame = getFrame target

            if frame.X <> start.X || frame.Y <> start.Y then
                moved <- true

            if moved then
                let p = Cursor.Position
                let screen = Screen.FromPoint p
                let gap = max 0 (getSettings ()).GapSize

                let snap =
                    snapZone (toRect screen.Bounds) p.X p.Y
                    |> Option.bind (fun a -> tile a 0 gap (toRect screen.WorkingArea) |> Option.map (fun r -> a, r))

                match snap with
                | Some(action, preview) ->
                    zone <- Some(action, screen)
                    overlay.ShowAt preview
                | None ->
                    zone <- None
                    overlay.HideOverlay()

    do timer.Tick.Add(fun _ -> update ())

    member _.Begin(hwnd: nativeint) =
        if (getSettings ()).DragToSnap
           && isManageable hwnd
           && not (isBorderHit hwnd Cursor.Position) then
            target <- hwnd
            start <- getFrame hwnd
            moved <- false
            zone <- None
            timer.Start()

    member _.End(hwnd: nativeint) =
        if hwnd = target then
            timer.Stop()
            overlay.HideOverlay()
            let finalZone = zone
            target <- 0n
            zone <- None

            match finalZone with
            | Some(action, screen) -> Manager.snapFromDrag (getSettings ()) hwnd action screen start
            | None when moved -> Manager.unsnapAfterDrag hwnd start Cursor.Position
            | None -> ()
