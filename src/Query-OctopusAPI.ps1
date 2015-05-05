Param(
    [Parameter(Mandatory=$true)]
    [string]$token,
    [Parameter(Mandatory=$true)]
    [string]$project,
    [Parameter(Mandatory=$true)]
    [string]$environment    
)

function Request-Data($token, $baseuri, $endpoint) {
    $headers= @{"X-Octopus-ApiKey"=$token}
    
    $loop = $true
    $uri = $endpoint

    while($loop) {    
        $return = (irm -Uri "$baseuri/$uri" -Headers $headers)
        $return

        if ($return.Links -and $return.Links."Page.Next"){
            $uri = $return.Links."Page.Next"                
            write-host "Next: $uri"
        }        
        else { $loop = $false}
    }
}

function Flatten-Items($pages){

    $pages | %{
        $page = $_
        $page.Items | foreach-object{
            $_
        }
        
    }    
}

function ConvertTo-DeploymentObject($items, [Hashtable]$lookup) {

    $items | foreach-object {
        $item = $_
        $o  = New-Object -TypeName PSObject
        $o | Add-Member -MemberType NoteProperty -Name _id -Value $_.Id
        
        $o | Add-Member -MemberType NoteProperty -Name releaseId -Value $_.ReleaseId
        if ($lookup.ContainsKey($_.ReleaseId)){ $o | Add-Member -MemberType NoteProperty -Name releaseName -Value $lookup[$_.ReleaseId] }
        else{ $o | Add-Member -MemberType NoteProperty -Name releaseName -Value "" }

        $o | Add-Member -MemberType NoteProperty -Name environmentId -Value $_.EnvironmentId
        
        if ($lookup.ContainsKey($_.EnvironmentId)){ $o | Add-Member -MemberType NoteProperty -Name environmentName -Value $lookup[$_.EnvironmentId]}
        else{ $o | Add-Member -MemberType NoteProperty -Name environmentName -Value "" }

        $o | Add-Member -MemberType NoteProperty -Name projectId -Value $_.ProjectId
        $o | Add-Member -MemberType NoteProperty -Name name -Value $_.Name        
        $o | Add-Member -MemberType NoteProperty -Name created -Value ([datetime]::ParseExact($_.Created, "yyyy-MM-ddTHH:mm:ss.fff+00:00", $null)).ToString("yyyy-MM-dd HH:mm:ss")        
        if ($_.LastModifiedOn){ $o | Add-Member -MemberType NoteProperty -Name lastModified -Value ([datetime]::ParseExact($_.LastModifiedOn, "yyyy-MM-ddTHH:mm:ss.fff+00:00", $null)).ToString("yyyy-MM-dd HH:mm:ss") }
        else { $o | Add-Member -MemberType NoteProperty -Name lastModified -Value ""}
        $o | Add-Member -MemberType NoteProperty -Name lastModifiedBy -Value $_.LastModifiedBy

        $o | Add-Member -MemberType NoteProperty -Name deploymentUnit -Value 1

        $o
    }
}

function Calculate-Metrics($items){

    $itemsWithCalculations = @()

    $sorted = $items | Sort-Object -Property created
    
    $sorted[0] | Add-Member -MemberType NoteProperty -Name daysSinceLastDeploy -Value 0
    $itemsWithCalculations += $sorted[0]

    $lastDeploy = $sorted[0].created

    $sorted | select-object -Skip 1 | foreach-object {
        $ts = New-TimeSpan -Start $lastDeploy -End $_.created

        $_ | Add-Member -MemberType NoteProperty -Name daysSinceLastDeploy -Value $ts.Days
        $itemsWithCalculations += $_

        $lastDeploy = $_.created
    } 

    $itemsWithCalculations

}


function Get-Data($token, $env, $project){
    
    $environments = (Request-Data $token "http://octopus.svc.dev.fundapps.co" "api/environments/all")    
    
    WRITE-host ("environments found: {0}" -f $environments.Count)
    
    $lookup = @{}

    $environments | foreach-object {
        $lookup.Add($_.Id, $_.Name)
    }

    $pages = (Request-Data $token "http://octopus.svc.dev.fundapps.co" "api/projects/$project/releases")
    $releases = Flatten-Items $pages
    
    WRITE-host ("releases found: {0}" -f $releases.Count)
    $relLookup = @{}
    $releases | foreach-object {
        $lookup.Add($_.Id, $_.Version)
    }

    $environments | %{        
        $env = $_        
        $pages = (Request-Data $token "http://octopus.svc.dev.fundapps.co" ("api/deployments?environments={0}" -f $env.Id))
        $deployments = Flatten-Items $pages
    
        WRITE-host ("deployments found for {0}: {1}" -f $env.Name,$deployments.Count)

        
        $items = (ConvertTo-DeploymentObject $deployments $lookup)
        
        Calculate-Metrics $items | Export-Csv "deployments.csv" -Append
     
    }

    
}


####################### MAIN ###################################

if (test-path deployments.csv) {rm deployments.csv -force}
Get-Data $token $environment $project
ii deployments.csv
