/// Pure layout math: given a screen's work area, where should a window go?
module Squarely.Geometry

open System
open Microsoft.FSharp.Reflection

[<Struct>]
type Rect =
    { X: int
      Y: int
      W: int
      H: int }

    member r.Right = r.X + r.W
    member r.Bottom = r.Y + r.H

    static member FromLTRB(left, top, right, bottom) =
        { X = left
          Y = top
          W = right - left
          H = bottom - top }

type WindowAction =
    | LeftHalf
    | RightHalf
    | CenterHalf
    | TopHalf
    | BottomHalf
    | TopLeft
    | TopRight
    | BottomLeft
    | BottomRight
    | FirstThird
    | CenterThird
    | LastThird
    | FirstTwoThirds
    | CenterTwoThirds
    | LastTwoThirds
    | FirstFourth
    | SecondFourth
    | ThirdFourth
    | LastFourth
    | FirstThreeFourths
    | LastThreeFourths
    | TopLeftSixth
    | TopCenterSixth
    | TopRightSixth
    | BottomLeftSixth
    | BottomCenterSixth
    | BottomRightSixth
    | Maximize
    | AlmostMaximize
    | MaximizeHeight
    | Larger
    | Smaller
    | Center
    | Restore
    | MoveLeft
    | MoveRight
    | MoveUp
    | MoveDown
    | NextDisplay
    | PreviousDisplay

let allActions: (string * WindowAction) list =
    FSharpType.GetUnionCases(typeof<WindowAction>)
    |> Array.map (fun c -> c.Name, FSharpValue.MakeUnion(c, [||]) :?> WindowAction)
    |> List.ofArray

let tryParseAction (name: string) =
    allActions
    |> List.tryFind (fun (n, _) -> String.Equals(n, name, StringComparison.OrdinalIgnoreCase))
    |> Option.map snd

let actionName (action: WindowAction) =
    allActions |> List.find (fun (_, a) -> a = action) |> fst

let inset (d: int) (r: Rect) =
    { X = r.X + d
      Y = r.Y + d
      W = max 1 (r.W - 2 * d)
      H = max 1 (r.H - 2 * d) }

let approxEqual tolerance (a: Rect) (b: Rect) =
    abs (a.X - b.X) <= tolerance
    && abs (a.Y - b.Y) <= tolerance
    && abs (a.W - b.W) <= tolerance
    && abs (a.H - b.H) <= tolerance

/// Move (and if necessary shrink) `r` so it lies entirely inside `area`.
let fit (area: Rect) (r: Rect) =
    let w = min r.W area.W
    let h = min r.H area.H

    { X = max area.X (min r.X (area.Right - w))
      Y = max area.Y (min r.Y (area.Bottom - h))
      W = w
      H = h }

/// Carry a window to another work area, keeping its size and its relative position.
let translate (src: Rect) (dst: Rect) (r: Rect) =
    let cx = float (r.X + r.W / 2 - src.X) / float src.W
    let cy = float (r.Y + r.H / 2 - src.Y) / float src.H

    fit
        dst
        { r with
            X = dst.X + int (cx * float dst.W) - r.W / 2
            Y = dst.Y + int (cy * float dst.H) - r.H / 2 }

/// A region of `area` described by fractions of its width and height.
let private section (area: Rect) x0 x1 y0 y1 =
    let px (f: float) = area.X + int (Math.Round(float area.W * f))
    let py (f: float) = area.Y + int (Math.Round(float area.H * f))
    Rect.FromLTRB(px x0, py y0, px x1, py y1)

/// Widths that repeated half-presses cycle through, as in Rectangle.
let private halfCycle = [| 1.0 / 2.0; 2.0 / 3.0; 1.0 / 3.0 |]

/// Target rect for actions that tile the window into a fixed region of the screen.
/// `cycle` counts how many times in a row the same action was repeated.
let tile (action: WindowAction) (cycle: int) (gap: int) (work: Rect) : Rect option =
    let area = inset (gap / 2) work
    let s = section area
    // Thirds, fourths and sixths split along the long axis, so portrait screens get rows.
    let portrait = area.H > area.W
    let along a b = if portrait then s 0.0 1.0 a b else s a b 0.0 1.0
    let nth n count = along (float n / float count) (float (n + 1) / float count)
    let half = halfCycle[cycle % halfCycle.Length]

    let sixth i =
        let cols, rows = if portrait then 2, 3 else 3, 2
        let c, r = i % cols, i / cols
        s (float c / float cols) (float (c + 1) / float cols) (float r / float rows) (float (r + 1) / float rows)

    let rect =
        match action with
        | LeftHalf -> Some(s 0.0 half 0.0 1.0)
        | RightHalf -> Some(s (1.0 - half) 1.0 0.0 1.0)
        | TopHalf -> Some(s 0.0 1.0 0.0 half)
        | BottomHalf -> Some(s 0.0 1.0 (1.0 - half) 1.0)
        | CenterHalf -> Some(s 0.25 0.75 0.0 1.0)
        | TopLeft -> Some(s 0.0 0.5 0.0 0.5)
        | TopRight -> Some(s 0.5 1.0 0.0 0.5)
        | BottomLeft -> Some(s 0.0 0.5 0.5 1.0)
        | BottomRight -> Some(s 0.5 1.0 0.5 1.0)
        | FirstThird -> Some(nth (cycle % 3) 3)
        | CenterThird -> Some(nth 1 3)
        | LastThird -> Some(nth (2 - cycle % 3) 3)
        | FirstTwoThirds -> Some(if cycle % 2 = 0 then along 0.0 (2.0 / 3.0) else along (1.0 / 3.0) 1.0)
        | CenterTwoThirds -> Some(along (1.0 / 6.0) (5.0 / 6.0))
        | LastTwoThirds -> Some(if cycle % 2 = 0 then along (1.0 / 3.0) 1.0 else along 0.0 (2.0 / 3.0))
        | FirstFourth -> Some(nth (cycle % 4) 4)
        | SecondFourth -> Some(nth 1 4)
        | ThirdFourth -> Some(nth 2 4)
        | LastFourth -> Some(nth (3 - cycle % 4) 4)
        | FirstThreeFourths -> Some(along 0.0 0.75)
        | LastThreeFourths -> Some(along 0.25 1.0)
        | TopLeftSixth -> Some(sixth 0)
        | TopCenterSixth -> Some(sixth 1)
        | TopRightSixth -> Some(sixth 2)
        | BottomLeftSixth -> Some(sixth 3)
        | BottomCenterSixth -> Some(sixth 4)
        | BottomRightSixth -> Some(sixth 5)
        | Maximize -> Some area
        | AlmostMaximize -> Some(s 0.05 0.95 0.05 0.95)
        | _ -> None

    rect |> Option.map (inset (gap / 2))

let private resizeStep = 30
let private minSize = 200

/// Target rect for actions that move or resize relative to the window's current frame.
let reposition (action: WindowAction) (work: Rect) (cur: Rect) : Rect option =
    let centerX w = work.X + (work.W - w) / 2
    let centerY h = work.Y + (work.H - h) / 2

    let rect =
        match action with
        | Center -> Some { cur with X = centerX cur.W; Y = centerY cur.H }
        | MaximizeHeight -> Some { cur with Y = work.Y; H = work.H }
        | Larger ->
            Some
                { X = cur.X - resizeStep
                  Y = cur.Y - resizeStep
                  W = cur.W + 2 * resizeStep
                  H = cur.H + 2 * resizeStep }
        | Smaller ->
            let w = max (min cur.W minSize) (cur.W - 2 * resizeStep)
            let h = max (min cur.H minSize) (cur.H - 2 * resizeStep)

            Some
                { X = cur.X + (cur.W - w) / 2
                  Y = cur.Y + (cur.H - h) / 2
                  W = w
                  H = h }
        | MoveLeft -> Some { cur with X = work.X; Y = centerY cur.H }
        | MoveRight -> Some { cur with X = work.Right - cur.W; Y = centerY cur.H }
        | MoveUp -> Some { cur with X = centerX cur.W; Y = work.Y }
        | MoveDown -> Some { cur with X = centerX cur.W; Y = work.Bottom - cur.H }
        | _ -> None

    rect |> Option.map (fit work)

/// Which action a drag should snap to when the cursor is at (x, y) on a monitor with `bounds`.
let snapZone (bounds: Rect) (x: int) (y: int) : WindowAction option =
    let edge, corner = 5, 30
    let left = x <= bounds.X + edge
    let right = x >= bounds.Right - 1 - edge
    let top = y <= bounds.Y + edge
    let bottom = y >= bounds.Bottom - 1 - edge
    let nearLeft = x <= bounds.X + corner
    let nearRight = x >= bounds.Right - 1 - corner
    let nearTop = y <= bounds.Y + corner
    let nearBottom = y >= bounds.Bottom - 1 - corner

    if (left && nearTop) || (top && nearLeft) then Some TopLeft
    elif (right && nearTop) || (top && nearRight) then Some TopRight
    elif (left && nearBottom) || (bottom && nearLeft) then Some BottomLeft
    elif (right && nearBottom) || (bottom && nearRight) then Some BottomRight
    elif top then Some Maximize
    elif left then Some LeftHalf
    elif right then Some RightHalf
    elif bottom then
        match (x - bounds.X) * 3 / bounds.W with
        | 0 -> Some FirstThird
        | 1 -> Some CenterThird
        | _ -> Some LastThird
    else
        None
