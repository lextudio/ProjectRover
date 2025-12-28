$origFile = 'tools/ApiDiff/reports/member-diff-config-full.txt'
$newFile  = 'tools/ApiDiff/reports/member-diff-config-2.txt'
$outFile  = 'tools/ApiDiff/reports/member-diff-summary.json'

function ParseReport($file){
  $lines = Get-Content $file
  $entries = @()
  for($i=0;$i -lt $lines.Count; $i++){
    $ln = $lines[$i]
    if($ln -match '^(\S+)\s+->\s+match:\s+(\S+)\s+; members: left=(\d+) right=(\d+) added=(\d+) removed=(\d+)'){
      $left = $matches[1]
      $right = $matches[2]
      $leftCount = [int]$matches[3]
      $rightCount = [int]$matches[4]
      $added = [int]$matches[5]
      $removed = [int]$matches[6]
      $entries += [pscustomobject]@{Left=$left; Right=$right; LeftCount=$leftCount; RightCount=$rightCount; Added=$added; Removed=$removed}
    }
  }
  return $entries
}

$orig = ParseReport $origFile
$new  = ParseReport $newFile

# build map by Left type
$origMap = @{}
foreach($e in $orig){$origMap[$e.Left] = $e}

$records = @()
foreach($e in $new){
  $l = $e.Left
  $o = $null
  if($origMap.ContainsKey($l)){ $o = $origMap[$l] }
  $rec = [pscustomobject]@{
    Left = $l
    Right = $e.Right
    OrigLeftCount = if($o){$o.LeftCount}else{0}
    OrigRightCount = if($o){$o.RightCount}else{0}
    OrigAdded = if($o){$o.Added}else{0}
    OrigRemoved = if($o){$o.Removed}else{0}
    NewLeftCount = $e.LeftCount
    NewRightCount = $e.RightCount
    NewAdded = $e.Added
    NewRemoved = $e.Removed
  }
  $rec | Add-Member -NotePropertyName DeltaAdded -NotePropertyValue ($rec.NewAdded - $rec.OrigAdded)
  $rec | Add-Member -NotePropertyName DeltaRemoved -NotePropertyValue ($rec.NewRemoved - $rec.OrigRemoved)
  $records += $rec
}

# stats
$related = $records.Count
$improved = ($records | Where-Object { $_.DeltaAdded -lt 0 -or $_.DeltaRemoved -lt 0 }).Count
$worse = ($records | Where-Object { $_.DeltaAdded -gt 0 -or $_.DeltaRemoved -gt 0 }).Count
$same = ($records | Where-Object { $_.DeltaAdded -eq 0 -and $_.DeltaRemoved -eq 0 }).Count
$totalDeltaAdded = ($records | Measure-Object -Property DeltaAdded -Sum).Sum
$totalDeltaRemoved = ($records | Measure-Object -Property DeltaRemoved -Sum).Sum

$result = [pscustomobject]@{
  RelatedTypes = $related
  ImprovedTypes = $improved
  WorseTypes = $worse
  Same = $same
  TotalDeltaAdded = $totalDeltaAdded
  TotalDeltaRemoved = $totalDeltaRemoved
  Records = $records
}

# write JSON (pretty)
$result | ConvertTo-Json -Depth 5 | Set-Content $outFile

# print summary and top 10 improvements
$result | Select-Object RelatedTypes,ImprovedTypes,WorseTypes,Same,TotalDeltaAdded,TotalDeltaRemoved | ConvertTo-Json -Depth 2
"`nTop 10 reductions by (DeltaAdded + DeltaRemoved):"
$records | ForEach-Object { $_ | Add-Member -NotePropertyName Score -NotePropertyValue (($_.DeltaAdded + $_.DeltaRemoved)) -PassThru } | Sort-Object Score | Select-Object -First 10 | Format-Table Left,Right,OrigAdded,NewAdded,DeltaAdded,OrigRemoved,NewRemoved,DeltaRemoved -AutoSize
