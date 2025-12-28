$a = (Get-Content 'tools/ApiDiff/reports/member-diff-config-full.txt' | Measure-Object -Line).Lines
$b = (Get-Content 'tools/ApiDiff/reports/member-diff-config-2.txt' | Measure-Object -Line).Lines
$result = [pscustomobject]@{OrigLines=$a; NewLines=$b; Delta=$b - $a}
$result | ConvertTo-Json -Depth 1
