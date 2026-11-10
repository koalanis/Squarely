/// Tray icon, menu, and process entry point.
module Squarely.App

open System
open System.Diagnostics
open System.Drawing
open System.Drawing.Drawing2D
open System.Reflection
open System.Text.RegularExpressions
open System.Threading
open System.Windows.Forms
open Microsoft.Win32
open Squarely.Native
open Squarely.Geometry
open Squarely.Config
open Squarely.Hotkeys
open Squarely.Overlay
open Squarely.DragSnap

let private runKey = @"Software\Microsoft\Windows\CurrentVersion\Run"

let private launchesAtLogin () =
    use key = Registry.CurrentUser.OpenSubKey runKey
    not (isNull key) && not (isNull (key.GetValue "Squarely"))

let private setLaunchAtLogin enabled =
    use key = Registry.CurrentUser.CreateSubKey runKey

    if enabled then
        key.SetValue("Squarely", sprintf "\"%s\"" Environment.ProcessPath)
    else
        key.DeleteValue("Squarely", false)

/// Windows' own Snap fights with drag-to-snap, so we offer a toggle for it.
let private windowsSnapEnabled () =
    let mutable v = 0
    SystemParametersInfoGet(SPI_GETWINARRANGING, 0u, &v, 0u) |> ignore
    v <> 0

let private setWindowsSnap on =
    SystemParametersInfoSet(SPI_SETWINARRANGING, (if on then 1u else 0u), 0n, SPIF_UPDATEINIFILE_SENDCHANGE)
    |> ignore

let private lightTaskbar () =
    use key =
        Registry.CurrentUser.OpenSubKey @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"

    not (isNull key)
    && (match key.GetValue "SystemUsesLightTheme" with
        | :? int as v -> v = 1
        | _ -> false)

/// A window outline with its left half filled, drawn to suit the taskbar theme.
let private makeIcon () =
    let color = if lightTaskbar () then Color.Black else Color.White
    use bmp = new Bitmap(32, 32)

    do
        use g = Graphics.FromImage bmp
        g.SmoothingMode <- SmoothingMode.AntiAlias
        use pen = new Pen(color, 2.5f)
        use brush = new SolidBrush(color)
        g.DrawRectangle(pen, 3, 6, 26, 20)
        g.FillRectangle(brush, 3, 6, 13, 20)

    let handle = bmp.GetHicon()
    let icon = (Icon.FromHandle handle).Clone() :?> Icon
    DestroyIcon handle |> ignore
    icon

let private displayName (action: WindowAction) =
    Regex.Replace(actionName action, "(?<=[a-z])(?=[A-Z])", " ")

type TrayApp() as this =
    inherit ApplicationContext()

    let loaded = Config.load ()
    let mutable settings = fst loaded
    let mutable lastForeground = GetForegroundWindow()

    let hotkeys = new HotkeyHost()
    let overlay = Overlay.create ()
    let snapper = DragSnapper(overlay, (fun () -> settings))
    let menu = new ContextMenuStrip()

    let tray =
        new NotifyIcon(Icon = makeIcon (), Text = "Squarely", ContextMenuStrip = menu)

    let warn (lines: string list) =
        if not lines.IsEmpty then
            tray.ShowBalloonTip(6000, "Squarely", String.Join("\n", lines), ToolTipIcon.Warning)

    let registerHotkeys () =
        hotkeys.Clear()

        [ for KeyValue(action, text) in settings.Shortcuts do
              if not (String.IsNullOrWhiteSpace text) then
                  match Hotkey.parse text with
                  | None -> yield sprintf "%s: can't understand shortcut \"%s\"" (actionName action) text
                  | Some hk ->
                      let ok =
                          hotkeys.Register(hk, (fun () -> Manager.run settings (GetForegroundWindow()) action))

                      if not ok then
                          yield sprintf "%s is used by another app (%s)" (Hotkey.format hk) (displayName action) ]

    let updateSettings f =
        settings <- f settings
        Config.save settings

    let buildMenu () =
        menu.Items.Clear()

        let actionItem action =
            let item = new ToolStripMenuItem(displayName action)

            match settings.Shortcuts.TryFind action |> Option.bind Hotkey.parse with
            | Some hk -> item.ShortcutKeyDisplayString <- Hotkey.format hk
            | None -> ()

            item.Click.Add(fun _ ->
                let hwnd = lastForeground
                Manager.run settings hwnd action
                SetForegroundWindow hwnd |> ignore)

            item :> ToolStripItem

        let submenu (title: string) actions =
            let item = new ToolStripMenuItem(title)
            item.DropDownItems.AddRange(actions |> List.map actionItem |> Array.ofList)
            item :> ToolStripItem

        let toggle (text: string) (isOn: unit -> bool) (set: bool -> unit) =
            let item = new ToolStripMenuItem(text, Checked = isOn ())

            item.Click.Add(fun _ ->
                set (not item.Checked)
                item.Checked <- isOn ())

            item :> ToolStripItem

        let command (text: string) (f: unit -> unit) =
            let item = new ToolStripMenuItem(text)
            item.Click.Add(fun _ -> f ())
            item :> ToolStripItem

        let sep () = new ToolStripSeparator() :> ToolStripItem

        let items =
            [ yield! [ LeftHalf; RightHalf; CenterHalf; TopHalf; BottomHalf ] |> List.map actionItem
              yield sep ()
              yield! [ TopLeft; TopRight; BottomLeft; BottomRight ] |> List.map actionItem
              yield sep ()
              yield!
                  [ FirstThird; CenterThird; LastThird; FirstTwoThirds; CenterTwoThirds; LastTwoThirds ]
                  |> List.map actionItem
              yield sep ()
              yield
                  submenu
                      "Fourths"
                      [ FirstFourth; SecondFourth; ThirdFourth; LastFourth; FirstThreeFourths; LastThreeFourths ]
              yield
                  submenu
                      "Sixths"
                      [ TopLeftSixth
                        TopCenterSixth
                        TopRightSixth
                        BottomLeftSixth
                        BottomCenterSixth
                        BottomRightSixth ]
              yield submenu "Move to Edge" [ MoveLeft; MoveRight; MoveUp; MoveDown ]
              yield sep ()
              yield! [ Maximize; AlmostMaximize; MaximizeHeight; Larger; Smaller; Center; Restore ] |> List.map actionItem
              yield sep ()
              yield! [ NextDisplay; PreviousDisplay ] |> List.map actionItem
              yield sep ()
              yield
                  toggle "Snap windows by dragging" (fun () -> settings.DragToSnap) (fun v ->
                      updateSettings (fun s -> { s with DragToSnap = v }))
              yield
                  toggle "Cycle sizes on repeated shortcuts" (fun () -> settings.CycleSizes) (fun v ->
                      updateSettings (fun s -> { s with CycleSizes = v }))
              yield toggle "Windows built-in Snap" windowsSnapEnabled setWindowsSnap
              yield toggle "Launch at login" launchesAtLogin setLaunchAtLogin
              yield sep ()
              yield
                  command "Edit Shortcuts && Settings..." (fun () ->
                      Process.Start(ProcessStartInfo(configPath, UseShellExecute = true)) |> ignore)
              yield command "Reload Settings" (fun () -> this.Reload())
              yield command "Quit Squarely" (fun () -> this.ExitThread()) ]

        menu.Items.AddRange(Array.ofList items)

    do
        hotkeys.Create()

        // Remember the last real app window so tray-menu actions target it, not the taskbar.
        WinEvents.subscribe EVENT_SYSTEM_FOREGROUND EVENT_SYSTEM_FOREGROUND (fun _ hwnd ->
            if WindowOps.isManageable hwnd then
                lastForeground <- hwnd)

        WinEvents.subscribe EVENT_SYSTEM_MOVESIZESTART EVENT_SYSTEM_MOVESIZEEND (fun evt hwnd ->
            if evt = EVENT_SYSTEM_MOVESIZESTART then
                snapper.Begin hwnd
            else
                snapper.End hwnd)

        // Rectangle opens its menu on a plain click, so do the same for left-click.
        tray.MouseUp.Add(fun e ->
            if e.Button = MouseButtons.Left then
                let show =
                    typeof<NotifyIcon>.GetMethod ("ShowContextMenu", BindingFlags.Instance ||| BindingFlags.NonPublic)

                if not (isNull show) then
                    show.Invoke(tray, null) |> ignore)

        let hotkeyWarnings = registerHotkeys ()
        buildMenu ()
        tray.Visible <- true
        warn (snd loaded @ hotkeyWarnings)

    member _.Reload() =
        let fresh, warnings = Config.load ()
        settings <- fresh
        let hotkeyWarnings = registerHotkeys ()
        buildMenu ()
        warn (warnings @ hotkeyWarnings)

    override _.ExitThreadCore() =
        WinEvents.unsubscribeAll ()
        hotkeys.Clear()
        hotkeys.DestroyHandle()
        tray.Visible <- false
        tray.Dispose()
        overlay.Dispose()
        base.ExitThreadCore()

[<EntryPoint; STAThread>]
let main _ =
    let mutable firstInstance = false
    use mutex = new Mutex(true, @"Local\Squarely.SingleInstance", &firstInstance)

    if firstInstance then
        Application.SetHighDpiMode HighDpiMode.PerMonitorV2 |> ignore
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault false
        use app = new TrayApp()
        Application.Run app

    0
