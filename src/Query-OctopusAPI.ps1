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
        $page.Items | %{
            $_
        }
        
    }    
}


function Get-Data($token, $env, $project){

    $uri = "http://octopus.svc.dev.fundapps.co/api/deployments?environments=$env"
    $headers= @{"X-Octopus-ApiKey"=$token}

    $pages = (Request-Data $token "http://octopus.svc.dev.fundapps.co" "api/deployments?environments=$env")
    $deployments = Flatten-Items $pages
    
    WRITE-host ("deployments found: {0}" -f $deployments.Count)


    
    $pages = (Request-Data $token "http://octopus.svc.dev.fundapps.co" "api/projects/$project/releases")
    $releases = Flatten-Items $pages

    WRITE-host ("releases found: {0}" -f $releases.Count)
}

Get-Data API-US3NDBOPZCB5MS0LE9DEH7R8B0Y Environments-225 projects-1 #Staging-AQR
#Get-Data Environments-226 //Production-AQR