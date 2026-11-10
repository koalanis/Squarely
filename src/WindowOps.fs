/// Reading and writing the frames of other applications' windows.
module Squarely.WindowOps

open System
open System.Drawing
open System.Runtime.InteropServices
open System.Text
open Squarely.Native
open Squarely.Geometry

let toRect (r: Rectangle) = { X = r.X; Y = r.Y; W = r.Width; H = r.Height }
let toRectangle (r: Rect) = Rectangle(r.X, r.Y, r.W, r.H)

let private ownPid = uint32 Environment.ProcessId

/// Desktop, taskbar, Start menu and other shell surfaces we must never move.
let private ignoredClasses =
    set
        [ "Progman"
          "WorkerW"
          "Shell_TrayWnd"
          "Shell_SecondaryTrayWnd"
          "Windows.UI.Core.CoreWindow"
          "NotifyIconOverflowWindow"
          "XamlExplorerHostIslandWindow"
          "TopLevelWindowForOverflowXamlIsland" ]

let className (hwnd: nativeint) =
    let sb = StringBuilder(256)
    GetClassName(hwnd, sb, sb.Capacity) |> ignore
    sb.ToString()

let isManageable (hwnd: nativeint) =
    if hwnd = 0n || not (IsWindowVisible hwnd) || hwnd = GetShellWindow() then
        false
    else
        let mutable pid = 0u
        GetWindowThreadProcessId(hwnd, &pid) |> ignore

        pid <> ownPid
        && (GetWindowLong(hwnd, GWL_STYLE) &&& WS_CHILD) = 0
        && not (ignoredClasses.Contains(className hwnd))

let private windowRect (hwnd: nativeint) =
    let mutable r = RECT()
    GetWindowRect(hwnd, &r) |> ignore
    r

/// The visible frame. On Windows 10/11 GetWindowRect includes invisible resize borders,
/// so we ask DWM for the bounds the user actually sees.
let private visibleRect (hwnd: nativeint) =
    let mutable r = RECT()

    if DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &r, Marshal.SizeOf<RECT>()) = 0 then
        r
    else
        windowRect hwnd

let getFrame (hwnd: nativeint) : Rect =
    let r = visibleRect hwnd
    Rect.FromLTRB(r.Left, r.Top, r.Right, r.Bottom)

/// Place the window so its *visible* frame matches `target`.
let setFrame (hwnd: nativeint) (target: Rect) =
    if IsZoomed hwnd || IsIconic hwnd then
        ShowWindow(hwnd, SW_RESTORE) |> ignore

    let place () =
        let outer = windowRect hwnd
        let visible = visibleRect hwnd
        let l = visible.Left - outer.Left
        let t = visible.Top - outer.Top
        let r = outer.Right - visible.Right
        let b = outer.Bottom - visible.Bottom

        SetWindowPos(
            hwnd,
            0n,
            target.X - l,
            target.Y - t,
            target.W + l + r,
            target.H + t + b,
            SWP_NOZORDER ||| SWP_NOACTIVATE ||| SWP_NOOWNERZORDER
        )
        |> ignore

    place ()
    // Crossing to a monitor with a different DPI makes the window rescale itself and its
    // borders, so the first placement can land wrong. A second pass corrects it.
    if not (approxEqual 1 (getFrame hwnd) target) then
        place ()

/// True when the point is on one of the window's resize borders, i.e. the user is resizing, not moving.
let isBorderHit (hwnd: nativeint) (p: Point) =
    let lParam = nativeint ((p.Y <<< 16) ||| (p.X &&& 0xFFFF))
    let mutable result = 0n
    let ok = SendMessageTimeout(hwnd, WM_NCHITTEST, 0n, lParam, SMTO_ABORTIFHUNG, 100u, &result)
    let hit = int result
    ok <> 0n && hit >= HTLEFT && hit <= HTBOTTOMRIGHT
