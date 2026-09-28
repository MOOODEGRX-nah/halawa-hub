# Halawa-Hub Xbox diagnostic - ASCII only, download-safe
$out = Join-Path $env:USERPROFILE ("Desktop\XboxDiag_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".txt")
function Log([string]$m) { Write-Host $m; Add-Content -Path $out -Value $m -Encoding UTF8 }
Log "==== Xbox Diag $(Get-Date) user=$env:USERNAME pc=$env:COMPUTERNAME ===="

Log "== 1. GamingRoot / XboxGames =="
$gr = 0
foreach ($d in (Get-PSDrive -PSProvider FileSystem)) {
  $root = $d.Root
  if (Test-Path (Join-Path $root ".GamingRoot")) {
    $gr++
    Log "GamingRoot on $root"
    $xg = Join-Path $root "XboxGames"
    if (Test-Path $xg) {
      foreach ($g in (Get-ChildItem $xg -Directory -ErrorAction SilentlyContinue)) {
        $cfg = Join-Path $g.FullName "Content\MicrosoftGame.config"
        if (Test-Path $cfg) { Log "  GAME(dir+config): $($g.Name)" } else { Log "  dir-no-config: $($g.Name)" }
      }
    } else { Log "  no XboxGames dir on $root" }
  }
}
if ($gr -eq 0) { Log "no .GamingRoot on any drive" }

Log "== 2. Windows.Games registry =="
$reg = "Registry::HKEY_CLASSES_ROOT\Extensions\ContractId\Windows.Games\PackageId"
if (Test-Path $reg) {
  $keys = @(Get-ChildItem $reg -ErrorAction SilentlyContinue)
  Log "contract keys: $($keys.Count)"
  $keys | Select-Object -First 8 | ForEach-Object { Log "  key: $($_.PSChildName)" }
} else { Log "contract key MISSING" }

Log "== 3. Appx packages =="
$ids = @{}
if (Test-Path $reg) { Get-ChildItem $reg -ErrorAction SilentlyContinue | ForEach-Object { $ids[$_.PSChildName] = 1 } }
$total = 0; $games = 0
foreach ($p in (Get-AppxPackage -ErrorAction SilentlyContinue)) {
  $total++
  if ($p.IsFramework -or $p.IsResourcePackage -or $p.IsBundle) { continue }
  $inContract = $ids.ContainsKey($p.PackageFullName) -or $ids.ContainsKey($p.PackageFamilyName)
  $inXboxGames = ($p.InstallLocation -like "*\XboxGames\*")
  $cfg = Join-Path $p.InstallLocation "MicrosoftGame.config"
  $hasCfg = Test-Path -LiteralPath $cfg -ErrorAction SilentlyContinue
  if ($inContract -or $inXboxGames -or $hasCfg) {
    $games++
    Log "  GAME: $($p.Name) | fam=$($p.PackageFamilyName) | loc=$($p.InstallLocation) | contract=$inContract xboxdir=$inXboxGames cfg=$hasCfg sig=$($p.SignatureKind)"
  }
}
Log "packages total=$total gameCandidates=$games"

Log "== 4. WindowsApps access =="
$wa = "C:\Program Files\WindowsApps"
if (Test-Path $wa) {
  $list = Get-ChildItem $wa -ErrorAction SilentlyContinue
  Log "WindowsApps exists, listable=$($null -ne $list)"
} else { Log "WindowsApps missing" }

Log "== done =="
Write-Host "REPORT SAVED: $out"
