﻿namespace DinoScientist

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

    type Page = {
        Number: int;
        Content: string;
    }

    type Action = 
    | Labeled
    | Unlabeled
    | Other of string

    type Event = {
        Id : int
        Name : string;
        EventDate : string;
        User : string;
        Action : Action;
    }

    type Issue = {
        id          : int;
        title       : string;
        state       : string;
        labels      : string seq;
        created     : DateTime;
        closed      : DateTime option;
        lastUpdated : DateTime;
        events      : Event seq option;
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
            let link = headers |> Seq.where (fun h -> h.Name = "Link") |> Seq.exactlyOne        
            this.getLastPage link

        member this.buildIssueEventsRequest (issueId : int) =     
            fun () ->
                let mutable request = new RestRequest();
                request.Resource <- (sprintf "/repos/fundapps/rapptr/issues/%i/events" issueId)
                request.AddQueryParameter("access_token",token.Value) |>ignore
                request.AddQueryParameter("per_page","50")  |> ignore
                request :> IRestRequest

        member this.buildIssuesByStatusRequest (state : IssueState)=     
            fun () ->
                let mutable request = new RestRequest();
                request.Resource <- "/repos/fundapps/rapptr/issues"
                request.AddQueryParameter("access_token",token.Value) |>ignore
                request.AddQueryParameter("state",state.Value) |> ignore
                request.AddQueryParameter("per_page","50")  |> ignore
                request :> IRestRequest
  
        member this.getPages (r : unit -> IRestRequest) : Page seq =        
            let rp = this.getResponse (r())
            seq {
                match (rp.Headers |> this.getPaginationInformation) with
                    | Some x -> 
                        match x with 
                        | Last p -> 
                            printfn "Number of pages: %i" p                    
                            for i in 1 .. p do                
                                let rp2 = r().AddQueryParameter("page",i.ToString()) |> this.getResponse      
                                yield {Number = i; Content = rp2.Content }                                                    
                        | _ -> failwith "this should never happen"
                    | None ->  yield { Number = 1; Content = rp.Content }
            }

        member this.extractLabels (l:JArray) = 
            l
            |> Seq.map(fun x -> x.ToObject<Dictionary<string,obj>>())
            |> Seq.map(fun x->(x.Item "name").ToString())

        member this.buildIssueEvents (content : string) : (Event seq) = 
            let labelEvents =
                content 
                |> JToken.Parse   
                |> Seq.map (fun x -> (x.ToObject<Dictionary<string,obj>>()))    
                |> Seq.where (fun i -> (i.["event"].ToString() = "labeled" || i.["event"].ToString() = "unlabeled" ))
            labelEvents
            |> Seq.map(fun i -> {   Id = i.["id"].ToString() |> Int32.Parse
                                    EventDate = i.["created_at"].ToString()
                                    Name =((i.["label"].ToString() |> JToken.Parse).ToObject<Dictionary<string,obj>>()).["name"].ToString()
                                    User = ((i.["actor"].ToString() |> JToken.Parse).ToObject<Dictionary<string,obj>>()).["login"].ToString()
                                    Action =    match i.["event"].ToString() with
                                                | "labeled"     -> Action.Labeled
                                                | "unlabeled"   -> Action.Unlabeled
                                                | o             -> Action.Other(o) })                 

        member this.getIssueEvents issueId =
            let r = this.getResponse (this.buildIssueEventsRequest issueId ())
            r.Content |> this.buildIssueEvents 

        member this.buildIssue (rawIssue : Dictionary<string,obj>) : Issue =
            let labels =  rawIssue.Item "labels":?>JArray |> this.extractLabels
            let created = (rawIssue.Item "created_at").ToString() |> DateTime.Parse
            let closed = (rawIssue.Item "closed_at")
            let lastUpdated = (rawIssue.Item "updated_at").ToString() |> DateTime.Parse
            let id = (rawIssue.Item "number").ToString()|>Int32.Parse
            let events = this.getIssueEvents id
            let state = (rawIssue.Item "state").ToString()

            {   id = id
                title = (rawIssue.Item "title").ToString()
                state = state
                labels = labels
                created = created
                lastUpdated = lastUpdated
                closed = match rawIssue.Item "closed_at" with | null -> None | dt -> Some(dt.ToString()|>DateTime.Parse) 
                events = if events |> Seq.isEmpty then None else Some(events) }

        member this.parseIssues (content : string) = 
            content
            |> JArray.Parse    
            |> Seq.map (fun x -> (x.ToObject<Dictionary<string,obj>>()))

        member this.getIssues (state : IssueState)=
            this.getPages (this.buildIssuesByStatusRequest state)        
            |> Seq.collect( fun p -> this.parseIssues p.Content)
            |> Seq.where(fun i -> not (i.ContainsKey("pull_request")))
            |> Seq.map this.buildIssue

