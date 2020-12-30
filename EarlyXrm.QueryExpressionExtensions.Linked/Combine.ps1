try {

    $path = "QueryExpressionExtensions.cs"
    If (Test-Path $path){
	    Remove-Item $path
    }

    write-host $PSScriptRoot
    $source = -join($PSScriptRoot, "\Source")

    Get-ChildItem -Path $source -Filter "*.cs" | ForEach-Object {

        write-host $_.FullName

        $usings += Get-Content $_.FullName | where-object {$_ -like "using *"}

        $data = Get-Content -Raw $_.FullName
        $first = $data.IndexOf("{")
        $last = $data.LastIndexOf("}")
        $next += $data.Substring($first + 1, $last - $first - 1)

    
    }

    write-host $path

    $usings = $usings | Sort-Object | Get-Unique
    Add-Content $path $usings
    Add-Content $path "`r`nnamespace EarlyXrm.QueryExpressionExtensions`r`n{"
    Add-Content $path $next
    Add-Content $path "}"

    write-host done
}
catch {
    write-host error
	exit 1
}