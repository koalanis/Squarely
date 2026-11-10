/// The translucent "footprint" that previews where a dragged window will snap.
module Squarely.Overlay

open System.Drawing
open System.Windows.Forms
open Squarely.Native
open Squarely.Geometry

// No `as this` here: Form's base constructor calls the CreateParams override, which would trip
// F#'s self-reference initialization check. Setup happens in `create` instead.
type Overlay private () =
    inherit Form()

    static member create() =
        let o = new Overlay()
        o.FormBorderStyle <- FormBorderStyle.None
        o.ShowInTaskbar <- false
        o.StartPosition <- FormStartPosition.Manual
        o.AutoScaleMode <- AutoScaleMode.None
        o.BackColor <- Color.FromArgb(48, 120, 220)
        o.Opacity <- 0.3
        o.TopMost <- true
        o.SetStyle(ControlStyles.OptimizedDoubleBuffer ||| ControlStyles.AllPaintingInWmPaint, true)
        o

    override _.ShowWithoutActivation = true

    override _.CreateParams =
        let cp = base.CreateParams
        // Click-through, never focused, absent from Alt+Tab.
        cp.ExStyle <-
            cp.ExStyle
            ||| WS_EX_LAYERED
            ||| WS_EX_TRANSPARENT
            ||| WS_EX_TOOLWINDOW
            ||| WS_EX_NOACTIVATE
            ||| WS_EX_TOPMOST

        cp

    override _.WndProc(m: Message byref) =
        // We position in physical pixels; don't let WinForms rescale us between monitors.
        if m.Msg <> WM_DPICHANGED then
            base.WndProc(&m)

    override this.OnPaint(e) =
        base.OnPaint e
        use pen = new Pen(Color.White, 6f)
        e.Graphics.DrawRectangle(pen, 3, 3, this.ClientSize.Width - 6, this.ClientSize.Height - 6)

    member this.ShowAt(r: Rect) =
        let target = Rectangle(r.X, r.Y, r.W, r.H)

        if this.Bounds <> target then
            this.Bounds <- target
            this.Invalidate()

        if not this.Visible then
            this.Show()

    member this.HideOverlay() =
        if this.Visible then
            this.Hide()
