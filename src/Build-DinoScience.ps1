<#
#optional: delete index

$indexName = "dinoscientist"
Invoke-WebRequest -method Delete -Uri "http://localhost:9200/$indexName"

#>


$indexName = "dinoscientist"
try {
    Invoke-WebRequest -method Head -Uri "http://localhost:9200/$indexName"
}
catch {
    $_.Exception
    "Not found, creating index"
    Invoke-WebRequest -method Put -Uri "http://localhost:9200/$indexName"
}



# create mappings

$mapping = @"
{
    "issue":{
        "properties":{
            "created":{"type":"date", "format" : "yyyy-MM-dd HH:mm:ss"},
            "closed":{"type":"date", "format" : "yyyy-MM-dd HH:mm:ss"},
            "lastUpdated":{"type":"date", "format" : "yyyy-MM-dd HH:mm:ss"},            
            "state":{"type":"string", "index":"not_analyzed"},
            "processState":{"type":"string", "index":"not_analyzed"},
            "tags":{"type":"string","index":"not_analyzed", "index_name":"tag"},
            "title":{"type":"string", "index":"analyzed"},
            "ageInDays": {
                  "type": "long"
               },
            "daysSinceLastUpdate": {
                  "type": "long"
               },
            "totalHoursWorked" :{
                "type":"float"
            }
               
    }
    
    }
}
"@

Invoke-WebRequest -method Put -Uri "http://localhost:9200/$indexName/_mapping/issue"  -body $mapping


$mapping = @"
{
    "pullrequest":{
        "properties":{
            "created":{"type":"date", "format" : "yyyy-MM-dd HH:mm:ss"},
            "closed":{"type":"date", "format" : "yyyy-MM-dd HH:mm:ss"},
            "lastUpdated":{"type":"date", "format" : "yyyy-MM-dd HH:mm:ss"},            
            "merged":{"type":"date", "format" : "yyyy-MM-dd HH:mm:ss"},
            "state":{"type":"string", "index":"not_analyzed"},
            "processState":{"type":"string", "index":"not_analyzed"},
            "tags":{"type":"string","index":"not_analyzed", "index_name":"tag"},
            "title":{"type":"string", "index":"analyzed"},
            "ageInDays": {
                  "type": "long"
               },
            "daysSinceLastUpdate": {
                  "type": "long"
               },
            "totalHoursWorked" :{
                "type":"float"
            }
               
    }
    
    }
}
"@

Invoke-WebRequest -method Put -Uri "http://localhost:9200/$indexName/_mapping/pullrequest"  -body $mapping


$mapping = @"
{
    "deployment":{
        "properties":{
            "releaseId":{"type":"string", "index":"not_analyzed"},
            "releaseName":{"type":"string", "index":"not_analyzed"},
            "environmentId":{"type":"string", "index":"not_analyzed"},
            "environmentName":{"type":"string", "index":"not_analyzed"},
            "projectId":{"type":"string", "index":"not_analyzed"},
            "name":{"type":"string", "index":"not_analyzed"},
            "created":{"type":"date", "format":"yyyy-MM-dd HH:mm:ss"},
            "lastModified":{"type":"date", "format":"yyyy-MM-dd HH:mm:ss"},
            "lastModifiedBy":{"type":"string", "index":"not_analyzed"},
            "deploymentUnit": {
                "type":"long"
            },
            "daysSinceLastDeploy" : {
                "type" : "float"
            }               
        }    
    }
}
"@

Invoke-WebRequest -method Put -Uri "http://localhost:9200/$indexName/_mapping/deployment"  -body $mapping