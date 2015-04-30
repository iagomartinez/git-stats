Param(
    [Parameter(Mandatory=$true)]
    [string]$jsonPath 
)

$dateformat = "yyyy-MM-dd HH:mm:ss"
$indexName = "dinoscientist_v2"
$type = "card"
$outfile = "cards.csv"


if (test-path $outfile) {rm $outfile -force}

$items = ((gc $jsonPath -raw) | convertfrom-json )
$items.Count
foreach($o in $items){
    #$o
    #$o.Closed
           
    $state = $o.State
    $created = [DateTime]::ParseExact($o.Created, $dateformat, $null)
    if ($o.Closed -eq $null -or $o.Closed -eq ""){
        $closed = $null
    }
    else {
        $closed = [DateTime]::ParseExact($o.Closed, $dateformat, $null)
    }
     
    $id = $o._id
       

    
    #$id
    
    #Invoke-WebRequest -method Put -Uri "http://localhost:9200/$indexName/$type/$id"  -body ($o |convertto-json)
    
                
    "{0},{1},{2:$dateformat},{3:$dateformat},{4},""{5}""" -f $o.repository.Name,$state,$created,$closed,$o._id,$o.title | tee $outfile -append
}