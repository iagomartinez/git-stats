namespace DinoScience

open RestSharp;
open System.Collections.Generic
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System;
open DinoScience.OctoStats;

[<RequireQualifiedAccess>]
module Core =    
    let public processLabels = Set.ofList ["blocked"; "planned"; "0 unplanned"; "1 now"; "2 next"; "3 soon"; "4 someday"; "5 maybe"; "in progress"; "ready for review"]    
    let public horizons = Set.ofList ["1 now"; "2 next"; "3 soon"; "4 someday"; "5 maybe"; "backlog" (*historic label*)]    

[<RequireQualifiedAccess>]
module Analytics =

    type PlanningHorizon =         
        | Unplanned
        | Maybe
        | Someday
        | Soon
        | Next
        | Now          
    
    type PlanningHorizon with
        static member Parse (input : string) : (PlanningHorizon) =
            match input with            
            | "1 now" | "backlog" -> Now
            | "2 next" -> Next
            | "3 soon" -> Soon
            | "4 someday" -> Someday
            | "5 maybe" -> Maybe
            | _ -> Unplanned

        member this.Value = 
            match this with
            | Unplanned     -> "Unplanned"
            | Maybe         -> "Maybe"
            | Someday       -> "Someday"
            | Soon          -> "Soon"
            | Next          -> "Next"
            | Now           -> "Now"            

    type ProcessState = 
        | Unplanned        
        | Maybe
        | Someday
        | Soon
        | Next
        | Now        
        | InProgress
        | ReadyForReview
        | Done
        | Incoming
        | Backlog
        | NotPrioritised
        | Blocked

    type ProcessState with
        member this.Value = 
            match this with
            | Unplanned     -> "Unplanned"
            | Maybe         -> "Maybe"
            | Someday       -> "Someday"
            | Soon          -> "Soon"
            | Next          -> "Next"
            | Now           -> "Now"
            | InProgress    -> "In progress"
            | ReadyForReview -> "Ready for review"
            | Done          -> "Done"
            | Incoming      -> "Incoming"
            | Backlog       -> "Backlog"
            | NotPrioritised-> "Not prioritised"
            | Blocked       -> "Blocked"
   

    let public inferState (labels : string seq) (issueState : IssueState) : ProcessState = 
        let contains (l : string) = labels |> Seq.exists (fun e -> e = l) 

        let hasNoProcessLabel = 
            Set.isEmpty <| (Set.intersect (labels |> Set.ofSeq)  (Core.processLabels |> Set.ofSeq)) 
        
        match issueState with  
        | Open when contains "blocked"                              -> Blocked
        | Open when contains "in progress"                          -> InProgress
        | Open when contains "1 now"                                -> Now
        | Open when contains "2 next"                               -> Next
        | Open when contains "3 soon"                               -> Soon
        | Open when contains "4 someday"                            -> Someday
        | Open when contains "5 maybe"                              -> Maybe
        | Open when contains "ready for review"                     -> ReadyForReview
        | Open when contains "backlog"                              -> Backlog
        | Open when contains "incoming"                             -> Incoming
        | Open                                                      -> NotPrioritised
        | Closed                                                    -> Done        

    let public firstHorizon (events : Event seq) : PlanningHorizon =
        let horizonEvents = 
            events
            |> Seq.where (fun e -> (match e.Action with | Action.Labeled when Core.horizons.Contains(e.Name) -> true | Action.Milestoned _ -> true | _ -> false))
        
        if (Seq.isEmpty horizonEvents) then
            PlanningHorizon.Unplanned
        else
            horizonEvents
            |> Seq.sortBy(fun e -> e.EventDate)
            |> Seq.head            
            |> (fun (e : Event) -> 
                match e.Action with
                | Action.Labeled -> PlanningHorizon.Parse e.Name
                | Action.Milestoned m ->
                    match m with
                    | "1 Now (Top 10)" |"Now (Top 10)" -> PlanningHorizon.Next
                    | "2 Next" -> PlanningHorizon.Soon
                    | "3 Soon" -> PlanningHorizon.Someday
                    | _ -> PlanningHorizon.Unplanned)


    let public calculateTotalDaysWorked (e : Event seq) : (float option) = 
        let daysWorked (wp : Event list) : (list<DateTime option * DateTime option>) =
            let rec daysWorked' (wp : Event list) (last : Event option) (r : list<DateTime option * DateTime option>) = 
                match wp with
                | []  when Option.isNone(last) -> []
                | []  ->  ((((match last with | Some s -> Some(s.EventDate|>DateTime.Parse) | None -> None), None)) :: r)
                | H :: T -> 
                    match H.Action with 
                    | Labeled -> daysWorked' T (Some(H)) r
                    | Unlabeled -> daysWorked' T (Some(H)) (((match last with | Some s -> Some(s.EventDate|>DateTime.Parse) | None -> None), (Some(H.EventDate|>DateTime.Parse))) :: r)
                    | _ -> failwith "unexpected action!"            
            daysWorked' wp None []

        let periods = e |> Seq.where (fun i -> i.Name = "in progress" && (i.Action = Action.Labeled && i.Name = "in progress" || i.Action = Action.Unlabeled))

        printfn "%A" e
        try
            let result = 
                daysWorked (periods|>List.ofSeq) 
                |> Seq.map(fun (t0, t1) -> 
                    let dt0 = (match t0 with |Some(d) -> d  | _ -> failwith "how did we end up here?")
                    let dt1 = (match t1 with |Some(d) -> d | None -> DateTime.Now)
                    (dt1 - dt0).TotalDays)
                |> Seq.fold (fun (next : float) (acc : float) -> acc + next) 0.0
            Some (result)
        with
            | ex -> None

module Parsers =

    type IssueCard = {
        _id                 : string;
        title               : string;
        state               : string;
        processState        : string;
        tags                : string seq;
        created             : string;
        closed              : string;
        lastUpdated         : string;
        ageInDays           : string;
        daysSinceLastUpdate : string;
        totalHoursWorked    : string;
        repository          : Repository;
        firstHorizon        : string;
    }
    
    type PullRequestCard = {
        _id                 : string;
        title               : string;
        state               : string;
        processState        : string;
        tags                : string seq;
        created             : string;
        closed              : string;
        lastUpdated         : string;
        merged              : string;
        ageInDays           : string;
        daysSinceLastUpdate : string;
        totalHoursWorked    : string;
        repository          : Repository;
    }
    
    let private excludeProcessLabels (labels) =
        Set.difference (labels |> Set.ofSeq) Core.processLabels

    let extractLabels (l:JArray) = 
        l
        |> Seq.map(fun x -> x.ToObject<Dictionary<string,obj>>())
        |> Seq.map(fun x->(x.Item "name").ToString())           

    let public parseIssue (issue : Issue) : IssueCard =
        let labels =  issue.labels
        let tags   = labels |> excludeProcessLabels
        let created = issue.created
        let closed = issue.closed
        let lastUpdated = issue.lastUpdated
        let closedDt = 
            match closed with
            | None -> DateTime.Now
            | Some dt -> dt

        let id = issue.id
        let events = 
            match issue.events with
            | Some e -> e
            | None  -> Seq.empty<Event>
            
        let state = issue.state

        {
            _id = (sprintf "%s_%i" issue.repository.Name id)
            title = issue.title
            processState = (state |> Analytics.inferState labels).Value
            state = state.Value
            tags = tags
            created = created.ToString("yyyy-MM-dd HH:mm:ss")
            lastUpdated = lastUpdated.ToString("yyyy-MM-dd HH:mm:ss")
            ageInDays = (closedDt - created).Days.ToString()
            daysSinceLastUpdate = (closedDt - lastUpdated).Days.ToString()
            closed = match closed with | None -> null | Some dt -> dt.ToString("yyyy-MM-dd HH:mm:ss")
            totalHoursWorked = match (events |> Analytics.calculateTotalDaysWorked) with |None -> null | Some v -> v.ToString()
            repository = issue.repository
            firstHorizon = (Analytics.firstHorizon events).Value
            }    
            
    let public parsePullRequest (pr: PullRequest) : PullRequestCard =
        let labels =  pr.labels
        let tags   = labels |> excludeProcessLabels
        let created = pr.created
        let closed = pr.closed
        let lastUpdated = pr.lastUpdated
        let closedDt = 
            match closed with
            | None -> DateTime.Now
            | Some dt -> dt

        let id = pr.id
        let events = 
            match pr.events with
            | Some e -> e
            | None  -> Seq.empty<Event>
            
        let state = pr.state

        {
            _id = (sprintf "%s_%i" pr.repository.Name id)
            title = pr.title
            processState = (state |> Analytics.inferState labels).Value
            state = state.Value
            tags = tags
            created = created.ToString("yyyy-MM-dd HH:mm:ss")
            lastUpdated = lastUpdated.ToString("yyyy-MM-dd HH:mm:ss")            
            ageInDays = (closedDt - created).Days.ToString()
            daysSinceLastUpdate = (closedDt - lastUpdated).Days.ToString()
            closed = match closed with | None -> null | Some dt -> dt.ToString("yyyy-MM-dd HH:mm:ss")
            merged = match pr.merged with | None -> null | Some dt -> dt.ToString("yyyy-MM-dd HH:mm:ss")
            totalHoursWorked = match (events |> Analytics.calculateTotalDaysWorked) with |None -> null | Some v -> v.ToString()
            repository = pr.repository
            }

