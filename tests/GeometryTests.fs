module Squarely.Tests.GeometryTests

open Xunit
open Squarely.Geometry

let work = { X = 0; Y = 0; W = 1920; H = 1040 }
let portrait = { X = 1920; Y = 0; W = 1080; H = 1880 }
let rect x y w h = { X = x; Y = y; W = w; H = h }

[<Fact>]
let ``left half cycles through 1/2, 2/3, 1/3`` () =
    Assert.Equal(Some(rect 0 0 960 1040), tile LeftHalf 0 0 work)
    Assert.Equal(Some(rect 0 0 1280 1040), tile LeftHalf 1 0 work)
    Assert.Equal(Some(rect 0 0 640 1040), tile LeftHalf 2 0 work)
    Assert.Equal(tile LeftHalf 0 0 work, tile LeftHalf 3 0 work)

[<Fact>]
let ``right half is anchored to the right edge`` () =
    Assert.Equal(Some(rect 960 0 960 1040), tile RightHalf 0 0 work)
    Assert.Equal(Some(rect 640 0 1280 1040), tile RightHalf 1 0 work)

[<Fact>]
let ``quarters`` () =
    Assert.Equal(Some(rect 960 520 960 520), tile BottomRight 0 0 work)

[<Fact>]
let ``thirds are columns on landscape and rows on portrait`` () =
    Assert.Equal(Some(rect 640 0 640 1040), tile CenterThird 0 0 work)
    Assert.Equal(Some(rect 1920 627 1080 626), tile CenterThird 0 0 portrait)

[<Fact>]
let ``first third cycles across the screen`` () =
    Assert.Equal(Some(rect 0 0 640 1040), tile FirstThird 0 0 work)
    Assert.Equal(Some(rect 640 0 640 1040), tile FirstThird 1 0 work)
    Assert.Equal(Some(rect 1280 0 640 1040), tile FirstThird 2 0 work)

[<Fact>]
let ``gaps leave a full gap at screen edges and between windows`` () =
    let left = (tile LeftHalf 0 10 work).Value
    let right = (tile RightHalf 0 10 work).Value
    Assert.Equal(10, left.X)
    Assert.Equal(10, left.Y)
    Assert.Equal(10, right.X - left.Right)
    Assert.Equal(10, work.Right - right.Right)

[<Fact>]
let ``secondary monitor offsets are respected`` () =
    let second = { X = -1280; Y = 200; W = 1280; H = 984 }
    Assert.Equal(Some(rect -1280 200 640 984), tile LeftHalf 0 0 second)

[<Fact>]
let ``center keeps size`` () =
    Assert.Equal(Some(rect 660 320 600 400), reposition Center work (rect 5 5 600 400))

[<Fact>]
let ``larger grows away from a screen edge`` () =
    let grown = (reposition Larger work (rect 0 100 800 600)).Value
    Assert.Equal(rect 0 70 860 660, grown)

[<Fact>]
let ``larger never exceeds the work area`` () =
    Assert.Equal(Some work, reposition Larger work (rect 0 0 1920 1040))

[<Fact>]
let ``smaller shrinks around the center but not below a minimum`` () =
    Assert.Equal(Some(rect 130 130 740 540), reposition Smaller work (rect 100 100 800 600))
    Assert.Equal(Some(rect 100 100 150 150), reposition Smaller work (rect 100 100 150 150))

[<Fact>]
let ``translate keeps size and relative position`` () =
    let other = { X = 1920; Y = 0; W = 1920; H = 1040 }
    Assert.Equal(rect 2020 100 800 600, translate work other (rect 100 100 800 600))

[<Fact>]
let ``snap zones`` () =
    let bounds = { X = 0; Y = 0; W = 1920; H = 1080 }
    Assert.Equal(Some Maximize, snapZone bounds 900 0)
    Assert.Equal(Some LeftHalf, snapZone bounds 0 500)
    Assert.Equal(Some RightHalf, snapZone bounds 1919 500)
    Assert.Equal(Some TopLeft, snapZone bounds 0 10)
    Assert.Equal(Some BottomRight, snapZone bounds 1919 1079)
    Assert.Equal(Some CenterThird, snapZone bounds 960 1079)
    Assert.Equal(None, snapZone bounds 900 500)

[<Fact>]
let ``every action name round-trips`` () =
    for name, action in allActions do
        Assert.Equal(Some action, tryParseAction (name.ToLowerInvariant()))
