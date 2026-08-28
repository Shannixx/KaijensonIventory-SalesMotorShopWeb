Start-Sleep -Seconds 5
try {
    $r = Invoke-WebRequest -Uri http://localhost:5230 -UseBasicParsing -Method GET
    Write-Output "Status:$($r.StatusCode)"
} catch {
    Write-Output "Error:$($_)"
}
