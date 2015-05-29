namespace DinoScience.Tests.``DinoScience Analytics tests``

open Swensen.Unquote
open Xunit
open DinoScience
open DinoScience.OctoStats;
    
module ``inferState tests`` = 
    [<Fact>]
    let ``infers in progress``() =
        let inferredState = Analytics.inferState ["in progress"] Open
        test <@ inferredState = Analytics.ProcessState.InProgress @>

    [<Fact>]
    let ``closed issue is Done regardless of labels``() =
        let inferredState = Analytics.inferState ["in progress"] Closed
        test <@ inferredState = Analytics.ProcessState.Done @>

    [<Fact>]
    let ``when state is open and labels contains "1 now" then state is Now``() =
        let inferredState = Analytics.inferState ["1 now"] Open
        test <@ inferredState = Analytics.ProcessState.Now @>
        
    [<Fact>]
    let ``when state is open and labels contains "2 next" then state is Next``() =
        let inferredState = Analytics.inferState ["2 next"] Open
        test <@ inferredState = Analytics.ProcessState.Next @>

    [<Fact>]
    let ``when state is open and labels contains "3 soon" then state is Soon``() =
        let inferredState = Analytics.inferState ["3 soon"] Open
        test <@ inferredState = Analytics.ProcessState.Soon @>
        
    [<Fact>]
    let ``when state is open and labels contains "4 someday" then state is Someday``() =
        let inferredState = Analytics.inferState ["4 someday"] Open
        test <@ inferredState = Analytics.ProcessState.Someday @>

    [<Fact>]
    let ``when state is open and labels contains "5 maybe" then state is Maybe``() =
        let inferredState = Analytics.inferState ["5 maybe"] Open
        test <@ inferredState = Analytics.ProcessState.Maybe @>
        
        
    [<Fact>]
    let ``when state is open and labels contains "ready for review" then state is Ready for Review``() =
        let inferredState = Analytics.inferState ["ready for review"] Open 
        test <@ inferredState = Analytics.ProcessState.ReadyForReview @>

    [<Fact>]
    let ``when state is open and labels do not contain any process labels then state is NotPrioritised``() =
        let inferredState = Analytics.inferState ["bug"; "code"] Open 
        test <@ inferredState = Analytics.ProcessState.NotPrioritised @>


    [<Fact>]
    let ``when state is open and labels contain "0 unplanned" and in progress then state is InProgress``() =
        let inferredState = Analytics.inferState ["0 unplanned"; "in progress"] Open 
        test <@ inferredState = Analytics.InProgress @>



    [<Fact>]
    let ``when state is open and labels contain "incoming" then state is Incoming``() =
        let inferredState = Analytics.inferState ["incoming"; "code"] Open 
        test <@ inferredState = Analytics.ProcessState.Incoming @>


    [<Fact>]
    let ``when state is open and labels contain "blocked" even if in progress state is Blocked``() =
        let inferredState = Analytics.inferState ["blocked"; "in progress"] Open 
        test <@ inferredState = Analytics.ProcessState.Blocked @>


    [<Fact>]
    let ``when state is open and labels contain "backlog" then state is Backlog``() =
        let inferredState = Analytics.inferState ["backlog"; "code"] Open 
        test <@ inferredState = Analytics.ProcessState.Backlog @>


module ``findFirstHorizonAssigned tests`` =

    [<Fact>]
    let ``construction test`` () =
        let events = 
            [ {Id=1; Name="1 now"; EventDate="2015-01-01";User="user";Action = Action.Labeled}
              {Id=1; Name="1 now"; EventDate="2015-01-02";User="user";Action = Action.Unlabeled}
              {Id=1; Name="in progress"; EventDate="2015-01-02";User="user";Action = Action.Labeled} ]

        let (firstHorizon : Analytics.PlanningHorizon) = Analytics.firstHorizon events
        test <@firstHorizon = Analytics.PlanningHorizon.Now@>

    [<Fact>]
    let ``case where went straight to in progress`` () =
        let events = 
            [ {Id=1; Name="in progress"; EventDate="2015-01-02";User="user";Action = Action.Labeled} ]

        let (firstHorizon : Analytics.PlanningHorizon) = Analytics.firstHorizon events
        test <@firstHorizon = Analytics.PlanningHorizon.Unplanned@>

    [<Fact>]
    let ``historical case where issue was first in "1 Now (Top 10)" milestone horizon is Next`` () =
        let events = 
            [ {Id=1; Name=null; EventDate="2015-01-02";User="user";Action = Action.Milestoned "1 Now (Top 10)"} ]

        let (firstHorizon : Analytics.PlanningHorizon) = Analytics.firstHorizon events
        test <@firstHorizon = Analytics.PlanningHorizon.Next@>


    [<Fact>]
    let ``historical case where issue was first in "Now (Top 10)" milestone horizon is Next`` () =
        let events = 
            [ {Id=1; Name=null; EventDate="2015-01-02";User="user";Action = Action.Milestoned "Now (Top 10)"} ]

        let (firstHorizon : Analytics.PlanningHorizon) = Analytics.firstHorizon events
        test <@firstHorizon = Analytics.PlanningHorizon.Next@>


    [<Fact>]
    let ``historical case where issue was first in "2 Next" milestone horizon is Soon`` () =
        let events = 
            [ {Id=1; Name=null; EventDate="2015-01-02";User="user";Action = Action.Milestoned "2 Next"} ]

        let (firstHorizon : Analytics.PlanningHorizon) = Analytics.firstHorizon events
        test <@firstHorizon = Analytics.PlanningHorizon.Soon@>


    [<Fact>]
    let ``historical case where issue was first in "3 Soon" milestone horizon is Someday`` () =
        let events = 
            [ {Id=1; Name=null; EventDate="2015-01-02";User="user";Action = Action.Milestoned "3 Soon"} ]

        let (firstHorizon : Analytics.PlanningHorizon) = Analytics.firstHorizon events
        test <@firstHorizon = Analytics.PlanningHorizon.Someday@>


    [<Fact>]
    let ``historical case where issue was first in backlog horizon is Now`` () =        
        let events = 
            [ {Id=1; Name= "backlog"; EventDate="2015-01-02";User="user";Action = Action.Labeled} ]

        let (firstHorizon : Analytics.PlanningHorizon) = Analytics.firstHorizon events
        test <@firstHorizon = Analytics.PlanningHorizon.Now@>