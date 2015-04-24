Param(
    [Parameter(Mandatory=$true)]
    [string]$jsonPath 
)

$dateformat = "dd/MM/yyyy HH:mm:ss"
$indexName = "dinoscientist_v2"
$type = "card"


if (test-path data.csv) {rm data.csv -force}

$items = ((gc $jsonPath -raw) | convertfrom-json )
$items.Count
foreach($o in $items){
    #$o.Closed
           
    $state = $o.State
    $created = [DateTime]::ParseExact($o.Created, $dateformat, $null)
    if ($o.Closed -eq $null -or $o.Closed -eq ""){
        $closed = ""
    }
    else {
        $closed = [DateTime]::ParseExact($o.Closed, $dateformat, $null)
    }
     
    $id = $o._id
       

    
    #$id
    
    Invoke-WebRequest -method Put -Uri "http://localhost:9200/$indexName/$type/$id"  -body ($o |convertto-json)
    
                
    #"{0},{1:$dateformat},{2},{3},{4}" -f $state, $created,$closed,$o._id,$o.title | ac test.csv 
}