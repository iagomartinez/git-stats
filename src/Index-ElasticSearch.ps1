Param(
    [Parameter(Mandatory=$true)]
    [string]$jsonPath,
    [Parameter(Mandatory=$true)]
    [string]$type
)

$dateformat = "yyyy-MM-dd HH:mm:ss"
$indexName = "dinoscientist"
$outfile = "cards.csv"


if (test-path $outfile) {rm $outfile -force}

$items = ((gc $jsonPath -raw) | convertfrom-json )

$ids = @{}

foreach($o in $items) {

<#           
    $state = $o.State
    $created = [DateTime]::ParseExact($o.Created, $dateformat, $null)
    if ($o.Closed -eq $null -or $o.Closed -eq ""){
        $closed = $null
    }
    else {
        $closed = [DateTime]::ParseExact($o.Closed, $dateformat, $null)
    }
     
    

    "{0},{1},{2:$dateformat},{3:$dateformat},{4},""{5}""" -f $o.repository.Name,$state,$created,$closed,$o._id,$o.title | tee $outfile -append
       
#>
    
    $id = $o._id
    Invoke-WebRequest -method Put -Uri "http://localhost:9200/$indexName/$type/$id"  -body ($o |convertto-json)
    
    if ($ids.ContainsKey($id)){$ids[$id]++}
}


$c = $items.Count
write-host "$c items indexed"