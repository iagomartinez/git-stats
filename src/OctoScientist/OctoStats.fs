namespace DinoScience

open RestSharp;
open System;
open System.Text.RegularExpressions;
open System.Collections.Generic;
open Newtonsoft.Json
open Newtonsoft.Json.Linq


module OctoStats =
    type ApiToken =
        | Token of string

    type ApiToken with
        member this.Value = match this with | Token t -> t

    type IssueState =
        | All
        | Open
        | Closed        

    type IssueState with
        member this.Value = 
            match this with
            | All   -> "all"
            | Open  -> "open"
            | Closed-> "closed"

        static member Parse (value : string) = 
            match value with
            | "all" -> All
            | "open" -> Open 
            | "closed" -> Closed
            | _ -> failwith ("unknown value " + value)

    type Page = {
        Number: int;
        Content: string;
    }

    type Action = 
    | Labeled
    | Unlabeled
    | Milestoned of string
    | Other of string

    type Event = {
        Id          : int
        Name        : string;
        EventDate   : string;
        User        : string;
        Action      : Action;
    }

    type Repository = {
        Name        : string
    }

    type Issue = {
        id          : int;
        title       : string;
        state       : IssueState;
        labels      : string seq;
        created     : DateTime;
        closed      : DateTime option;
        lastUpdated : DateTime;
        events      : Event seq option;
        repository  : Repository;
    }

    type PullRequest = {
        id          : int;
        title       : string;
        state       : IssueState;
        labels      : string seq;
        created     : DateTime;
        closed      : DateTime option;
        lastUpdated : DateTime;
        merged      : DateTime option;
        events      : Event seq option;
        repository  : Repository;     
    }

    type private PageRel = 
        | Last of int
        | Other
    
    type GitHubApiSession (token : ApiToken) =
        let token = token
        let mutable client = new RestSharp.RestClient(@"https://api.github.com/")
                   
        member private this.getResponse (r : IRestRequest) =
            let response = r |> client.Execute
            printfn "request: %A" (client.BuildUri(r))
            if response.StatusCode <> Net.HttpStatusCode.OK then failwith (sprintf "request failed with code %s" (response.StatusCode.ToString()))
            response
    
        member private this.getLastPage (headerLink:Parameter) : PageRel option =
            let r = new Regex("(?<page>\d{1,})>;\srel=\"last\"")
            let link = headerLink.Value.ToString()
            match r.IsMatch(link) with
            | true -> 
                let rm = r.Match(link)
                Some (Last(rm.Groups.["page"].Value |> Int32.Parse ))
            | false -> None         

        member private this.getPaginationInformation (headers:IList<Parameter>) =
            headers 
            |> Seq.tryFind (fun h -> h.Name = "Link")
            |> Option.bind (fun (h : Parameter) -> this.getLastPage h)
            

        member this.getPages (r : unit -> IRestRequest) : Async<Page> seq =        
            let rp = this.getResponse (r())
            seq {
                match (rp.Headers |> this.getPaginationInformation) with
                    | Some x -> 
                        match x with 
                        | Last p -> 
                            printfn "Number of pages: %i" p                    
                            for i in 1 .. p do                
                                yield async {
                                        let rp2 = (r().AddQueryParameter("page",i.ToString()) |> this.getResponse)
                                        return { Number = i; Content = rp2.Content }                                                    
                                    }                                
                        | _ -> failwith "this should never happen"
                    | None ->  yield async {
                            return { Number = 1; Content = rp.Content }
                        }
            }   

        member this.buildIssueEventsRequest (repoName : string)  (issueId : int) =
            fun () ->
                let mutable request = new RestRequest();
                request.Resource <- (sprintf "/repos/fundapps/%s/issues/%i/events" repoName issueId)
                request.AddQueryParameter("access_token",token.Value) |>ignore
                request.AddQueryParameter("per_page","50")  |> ignore
                request :> IRestRequest

        member this.buildIssuesApiRequest (repoName : string) (state : IssueState) =     
            fun () ->
                let mutable request = new RestRequest();
                request.Resource <- (sprintf "/repos/fundapps/%s/issues" repoName)
                request.AddQueryParameter("access_token",token.Value) |>ignore
                request.AddQueryParameter("state",state.Value) |> ignore
                request.AddQueryParameter("per_page","50")  |> ignore
                request :> IRestRequest

        member this.buildPullsApiRequest (repoName : string) (state : IssueState) =     
            fun () ->
                let mutable request = new RestRequest();
                request.Resource <- (sprintf "/repos/fundapps/%s/pulls" repoName)
                request.AddQueryParameter("access_token",token.Value) |>ignore
                request.AddQueryParameter("state",state.Value) |> ignore
                request.AddQueryParameter("per_page","50")  |> ignore
                request :> IRestRequest
  
        member this.parseContent (content : string) = 
            content
            |> JArray.Parse    
            |> Seq.map (fun x -> (x.ToObject<Dictionary<string,obj>>()))

        member this.extractLabels (l:JArray) = 
            l
            |> Seq.map(fun x -> x.ToObject<Dictionary<string,obj>>())
            |> Seq.map(fun x->(x.Item "name").ToString())

        member this.buildIssueEvents (content : string) : (Event seq) = 
            let isRelevantEvent (e : string) =
                (e = "labeled" || e = "unlabeled" || e = "milestoned")
            
            let labelEvents =
                content 
                |> JToken.Parse   
                |> Seq.map (fun x -> (x.ToObject<Dictionary<string,obj>>()))    
                |> Seq.where (fun i -> i.["event"].ToString() |> isRelevantEvent)

            let extractSubfield (fields : Dictionary<string, obj>) (field : string) (subfield : string) : string =
                if (fields.ContainsKey(field)) then
                    ((fields.[field].ToString() |> JToken.Parse).ToObject<Dictionary<string,obj>>()).[subfield].ToString()
                else
                    null

            labelEvents
            |> Seq.map(fun i -> {   Id = i.["id"].ToString() |> Int32.Parse
                                    EventDate = i.["created_at"].ToString()
                                    Name = extractSubfield i "label" "name"
                                    User = extractSubfield i "actor" "login"
                                    Action =    match i.["event"].ToString() with
                                                | "labeled"     -> Action.Labeled
                                                | "unlabeled"   -> Action.Unlabeled
                                                | "milestoned"  -> Action.Milestoned (extractSubfield i "milestone" "title")
                                                | o             -> Action.Other(o) })                 

        member this.getIssueEvents (repoName : string) (issueId : int) =
            async {
                let r = this.getResponse (this.buildIssueEventsRequest repoName issueId ())
                return (r.Content |> this.buildIssueEvents)
            }

        member this.buildIssue (repoName : string) (rawIssue : Dictionary<string,obj>) : Issue =
            let labels =  rawIssue.Item "labels":?>JArray |> this.extractLabels
            let created = (rawIssue.Item "created_at").ToString() |> DateTime.Parse
            let lastUpdated = (rawIssue.Item "updated_at").ToString() |> DateTime.Parse
            let id = (rawIssue.Item "number").ToString()|>Int32.Parse
            let events = (this.getIssueEvents repoName id |> Async.RunSynchronously)
            let state = (rawIssue.Item "state").ToString()

            {   id = id
                title = (rawIssue.Item "title").ToString()
                state = IssueState.Parse state
                labels = labels
                created = created
                lastUpdated = lastUpdated
                closed = match rawIssue.Item "closed_at" with | null -> None | dt -> Some(dt.ToString()|>DateTime.Parse) 
                events = if events |> Seq.isEmpty then None else Some(events) 
                repository = {  Name = repoName }
                }

        member this.parsePullRequest (repoName : string) (raw : Dictionary<string,obj>) : PullRequest =                        
            {   id          = (raw.Item "number").ToString()|>Int32.Parse
                title       = (raw.Item "title").ToString()
                state       = (raw.Item "state").ToString() |> IssueState.Parse
                labels      = []
                created     = (raw.Item "created_at").ToString() |> DateTime.Parse
                closed      = match raw.Item "closed_at" with | null -> None | dt -> Some(dt.ToString()|>DateTime.Parse) 
                lastUpdated = (raw.Item "updated_at").ToString() |> DateTime.Parse
                merged      = match raw.Item "merged_at" with | null -> None | dt -> Some(dt.ToString()|>DateTime.Parse) 
                events      = None
                repository  = { Name = repoName} }

        member this.getIssues (repoName: string) (state : IssueState) =
            this.getPages (this.buildIssuesApiRequest repoName state) 
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Seq.collect( fun p -> this.parseContent p.Content)
            |> Seq.where(fun i -> not (i.ContainsKey("pull_request")))
            |> Seq.map (this.buildIssue repoName)

        member this.getIssuesAsync (repoName: string) (state : IssueState) : (Async<Issue seq>)  =
            async {
                return this.getPages (this.buildIssuesApiRequest repoName state) 
                    |> Async.Parallel
                    |> Async.RunSynchronously
                    |> Seq.collect( fun p -> this.parseContent p.Content)
                    |> Seq.where(fun i -> not (i.ContainsKey("pull_request")))
                    |> Seq.map (this.buildIssue repoName)
            }
            
        member this.getPullsAsync (repoName: string) (state : IssueState) : (Async<PullRequest seq>) =
            async {
                return this.getPages (this.buildPullsApiRequest repoName state) 
                    |> Async.Parallel
                    |> Async.RunSynchronously
                    |> Seq.collect( fun p -> this.parseContent p.Content)
                    |> Seq.map (this.parsePullRequest repoName)
            }
