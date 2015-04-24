module Tests.``Function tests``

open Xunit
open Swensen.Unquote
open OctoScientist.Core


[<Fact>]
let ``infers in progress when labels contain "in progress"``() =
    let s = inferState ["in progress"] "open"
    test <@ s = "In Progress"@>

[<Fact>]
let ``infers backlog when labels contain "backlog"``() =
    let s = inferState ["backlog"] "open"
    test <@ s = "Backlog"@>

[<Fact>]
let ``infers Ready for Review when labels contain "ready for review"``() =
    let s = inferState ["ready for review"] "open"
    test <@ s = "Ready for Review"@>

[<Fact>]
let ``infers "Ideas" when labels do not contain any card states``() =
    let s = inferState ["requested by customer"; "bug"] "open"
    test <@ s = "Ideas"@>

[<Fact>]
let ``infers "Ideas" when labels are empty``() =
    let s = inferState [] "open"
    test <@ s = "Ideas"@>