Param(
    [Parameter(Mandatory=$true)]
    [string]$filePath,
    [Parameter(Mandatory=$true)]
    [string]$typeName
)

$dateformat = "yyyy-MM-dd HH:mm:ss"
$indexName = "dinoscientist"
#$outfile = "cards.csv"


#if (test-path $outfile) {rm $outfile -force}

switch -regex ($filePath){
    ".json$" { $items = ((gc $filePath -raw) | convertfrom-json ) }
    ".csv$"  { $items = Import-CSV $filePath }
    default  { throw "unknown file format!" }
}


write-host "{0} items found in file {1}" -f $items.Count $filePath

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
    $o
    
    $id = $o._id
    Invoke-WebRequest -method Put -Uri "http://localhost:9200/$indexName/$typeName/$id"  -body ($o |convertto-json)
    
    if ($ids.ContainsKey($id)){$ids[$id]++}
}


$c = $items.Count
write-host "$c items indexed"