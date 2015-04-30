namespace DinoScientist

open RestSharp;
open System.Collections.Generic
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System;
open DinoScientist.OctoStats;
//open FSharp.Data;

//[<RequireQualifiedAccess>]
module Core =    
    let public cardStates = Set.ofList ["in progress";"backlog";"ready for review"]

    let public inferState (labels : string seq) = function    
        | "open" when labels |> Seq.exists (fun e -> e ="in progress")          -> "In Progress"
        | "open" when labels |> Seq.exists (fun e -> e ="backlog")              -> "Backlog"
        | "open" when labels |> Seq.exists (fun e -> e ="ready for review")     -> "Ready for Review"
        | "open" when Set.ofSeq labels |> Set.intersect cardStates |>Seq.isEmpty-> "Ideas"
        | "closed"                                                              -> "Done"
        | _                                                                     -> "Unknown"

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

    let totalDaysWorked (e : Event seq) : (float option) = 
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
            
    let extractLabels (l:JArray) = 
        l
        |> Seq.map(fun x -> x.ToObject<Dictionary<string,obj>>())
        |> Seq.map(fun x->(x.Item "name").ToString())           

    let public convertToCard (issue : Issue) : IssueCard =
        let labels =  issue.labels
        let tags   = Set.difference (labels |> Set.ofSeq) cardStates 
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
            processState = inferState labels state
            state = state
            tags = tags
            created = created.ToString("yyyy-MM-dd HH:mm:ss")
            lastUpdated = lastUpdated.ToString("yyyy-MM-dd HH:mm:ss")
            ageInDays = (closedDt - created).Days.ToString()
            daysSinceLastUpdate = (closedDt - lastUpdated).Days.ToString()
            closed = match closed with | None -> null | Some dt -> dt.ToString("yyyy-MM-dd HH:mm:ss")
            totalHoursWorked = match (events |> totalDaysWorked) with |None -> null | Some v -> v.ToString()
            repository = issue.repository
            }    
            
    let public convertToPullRequest (pr: PullRequest) : PullRequestCard =
        let labels =  pr.labels
        let tags   = Set.difference (labels |> Set.ofSeq) cardStates 
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
            processState = inferState labels state
            state = state
            tags = tags
            created = created.ToString("yyyy-MM-dd HH:mm:ss")
            lastUpdated = lastUpdated.ToString("yyyy-MM-dd HH:mm:ss")            
            ageInDays = (closedDt - created).Days.ToString()
            daysSinceLastUpdate = (closedDt - lastUpdated).Days.ToString()
            closed = match closed with | None -> null | Some dt -> dt.ToString("yyyy-MM-dd HH:mm:ss")
            merged = match pr.merged with | None -> null | Some dt -> dt.ToString("yyyy-MM-dd HH:mm:ss")
            totalHoursWorked = match (events |> totalDaysWorked) with |None -> null | Some v -> v.ToString()
            repository = pr.repository
            }

