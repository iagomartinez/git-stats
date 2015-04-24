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

let session = new GitHubApiSession(ApiToken.Token(""))

let cards = 
    IssueState.Open 
    |> session.getIssues 
    |> Seq.map (convertToCard)

cards
|> Seq.length
|> printfn "count: %i" 
       
let json = cards |> JsonConvert.SerializeObject

File.WriteAllText(@"test.json", json)