/// Raw Win32 interop. Nothing clever lives here.
module Squarely.Native

open System
open System.Runtime.InteropServices
open System.Text

[<Struct; StructLayout(LayoutKind.Sequential)>]
type RECT =
    val mutable Left: int
    val mutable Top: int
    val mutable Right: int
    val mutable Bottom: int

type WinEventProc = delegate of nativeint * uint32 * nativeint * int * int * uint32 * uint32 -> unit

[<Literal>]
let GWL_STYLE = -16
[<Literal>]
let WS_CHILD = 0x40000000
[<Literal>]
let SW_RESTORE = 9
[<Literal>]
let SWP_NOZORDER = 0x0004u
[<Literal>]
let SWP_NOACTIVATE = 0x0010u
[<Literal>]
let SWP_NOOWNERZORDER = 0x0200u
[<Literal>]
let DWMWA_EXTENDED_FRAME_BOUNDS = 9

[<Literal>]
let WM_HOTKEY = 0x0312
[<Literal>]
let WM_NCHITTEST = 0x0084u
[<Literal>]
let WM_DPICHANGED = 0x02E0
[<Literal>]
let MOD_ALT = 0x0001u
[<Literal>]
let MOD_CONTROL = 0x0002u
[<Literal>]
let MOD_SHIFT = 0x0004u
[<Literal>]
let MOD_WIN = 0x0008u
[<Literal>]
let MOD_NOREPEAT = 0x4000u

[<Literal>]
let EVENT_SYSTEM_FOREGROUND = 0x0003u
[<Literal>]
let EVENT_SYSTEM_MOVESIZESTART = 0x000Au
[<Literal>]
let EVENT_SYSTEM_MOVESIZEEND = 0x000Bu
[<Literal>]
let WINEVENT_OUTOFCONTEXT = 0x0000u
[<Literal>]
let WINEVENT_SKIPOWNPROCESS = 0x0002u
[<Literal>]
let OBJID_WINDOW = 0
[<Literal>]
let CHILDID_SELF = 0

[<Literal>]
let HTLEFT = 10
[<Literal>]
let HTBOTTOMRIGHT = 17
[<Literal>]
let SMTO_ABORTIFHUNG = 0x0002u

[<Literal>]
let SPI_GETWINARRANGING = 0x0082u
[<Literal>]
let SPI_SETWINARRANGING = 0x0083u
[<Literal>]
let SPIF_UPDATEINIFILE_SENDCHANGE = 0x0003u

[<Literal>]
let WS_EX_TOPMOST = 0x00000008
[<Literal>]
let WS_EX_TRANSPARENT = 0x00000020
[<Literal>]
let WS_EX_TOOLWINDOW = 0x00000080
[<Literal>]
let WS_EX_LAYERED = 0x00080000
[<Literal>]
let WS_EX_NOACTIVATE = 0x08000000

[<DllImport("user32.dll")>]
extern nativeint GetForegroundWindow()

[<DllImport("user32.dll")>]
extern bool SetForegroundWindow(nativeint hWnd)

[<DllImport("user32.dll")>]
extern nativeint GetShellWindow()

[<DllImport("user32.dll")>]
extern bool IsWindow(nativeint hWnd)

[<DllImport("user32.dll")>]
extern bool IsWindowVisible(nativeint hWnd)

[<DllImport("user32.dll")>]
extern bool IsZoomed(nativeint hWnd)

[<DllImport("user32.dll")>]
extern bool IsIconic(nativeint hWnd)

[<DllImport("user32.dll")>]
extern bool ShowWindow(nativeint hWnd, int nCmdShow)

[<DllImport("user32.dll")>]
extern bool GetWindowRect(nativeint hWnd, RECT& lpRect)

[<DllImport("user32.dll", SetLastError = true)>]
extern bool SetWindowPos(nativeint hWnd, nativeint hWndInsertAfter, int x, int y, int cx, int cy, uint32 uFlags)

[<DllImport("user32.dll")>]
extern int GetWindowLong(nativeint hWnd, int nIndex)

[<DllImport("user32.dll", CharSet = CharSet.Unicode)>]
extern int GetClassName(nativeint hWnd, StringBuilder lpClassName, int nMaxCount)

[<DllImport("user32.dll")>]
extern uint32 GetWindowThreadProcessId(nativeint hWnd, uint32& lpdwProcessId)

[<DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")>]
extern nativeint SendMessageTimeout(nativeint hWnd, uint32 msg, nativeint wParam, nativeint lParam, uint32 fuFlags, uint32 uTimeout, nativeint& lpdwResult)

[<DllImport("user32.dll", SetLastError = true)>]
extern bool RegisterHotKey(nativeint hWnd, int id, uint32 fsModifiers, uint32 vk)

[<DllImport("user32.dll")>]
extern bool UnregisterHotKey(nativeint hWnd, int id)

[<DllImport("user32.dll")>]
extern nativeint SetWinEventHook(uint32 eventMin, uint32 eventMax, nativeint hmodWinEventProc, WinEventProc lpfnWinEventProc, uint32 idProcess, uint32 idThread, uint32 dwFlags)

[<DllImport("user32.dll")>]
extern bool UnhookWinEvent(nativeint hWinEventHook)

[<DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")>]
extern bool SystemParametersInfoGet(uint32 uiAction, uint32 uiParam, int& pvParam, uint32 fWinIni)

[<DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")>]
extern bool SystemParametersInfoSet(uint32 uiAction, uint32 uiParam, nativeint pvParam, uint32 fWinIni)

[<DllImport("user32.dll")>]
extern bool DestroyIcon(nativeint hIcon)

[<DllImport("dwmapi.dll")>]
extern int DwmGetWindowAttribute(nativeint hwnd, int dwAttribute, RECT& pvAttribute, int cbAttribute)
