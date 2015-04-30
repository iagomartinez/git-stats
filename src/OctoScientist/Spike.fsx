#r @"C:\code\git-stats\src\OctoScientist\packages\RestSharp.105.0.1\lib\net4\RestSharp.dll"
#r @"C:\code\git-stats\src\OctoScientist\packages\Newtonsoft.Json.6.0.8\lib\net45\Newtonsoft.Json.dll"
#load "OctoStats.fs"
#load "Core.fs"

open RestSharp;
open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Collections.Generic
open System.Text.RegularExpressions
open System.IO;

open DinoScientist.OctoStats;
open DinoScientist.Core;


//////////////////////////////////////////////////////////////////////
//  Execution


let repos = ["rules";"rapptr";"regulatorydata"]

let issues () =     
    repos
    |> Seq.map(fun repo -> session.getIssuesAsync repo IssueState.All)
    |> Async.Parallel
    |> Async.RunSynchronously
    |> Seq.collect (fun issues -> issues|>Seq.map(convertToCard))

let pulls () = 
    repos
    |> Seq.map(fun repo -> session.getPullsAsync repo IssueState.All)
    |> Async.Parallel
    |> Async.RunSynchronously
    |> Seq.collect (fun issues -> issues|>Seq.map(convertToPullRequest))

let run (cards : seq<'a>) (outfile : string) =
    cards
    |> Seq.length
    |> printfn "count: %i"            
    let json = cards |> JsonConvert.SerializeObject
    File.WriteAllText(outfile, json)


////////////////////////////////////////////////////////////////////
//  Main

run (issues ()) @"issues.json"
run (pulls ()) @"pullrequests.json"
