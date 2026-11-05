<#
.SYNOPSIS
  Creates an Odisea program from a spec file (see ../reference/spec-format.md).

.DESCRIPTION
  Resolves or creates the destination and hotels, creates the program with its content and
  itinerary in one call, adds departures, attaches hotels, uploads images, and prints the
  readiness report. Works on Windows PowerShell 5.1 and PowerShell 7; image uploads use curl.exe.

.EXAMPLE
  pwsh Import-ProgramSpec.ps1 -Spec program-spec.json -DryRun
  pwsh Import-ProgramSpec.ps1 -Spec program-spec.json -BaseUrl http://localhost:8090 -Email admin@odisea.local -Password '...'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Spec,
    [string] $BaseUrl = "http://localhost:8090",
    [string] $Token,
    [string] $Email,
    [string] $Password,
    [switch] $DryRun,
    [switch] $Publish
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
$specPath = (Resolve-Path $Spec).Path
$specDir = Split-Path $specPath -Parent
$doc = Get-Content -Raw -Encoding UTF8 $specPath | ConvertFrom-Json

# ---------- local validation ----------
$problems = New-Object System.Collections.Generic.List[string]
function Fail([string] $message) { $problems.Add($message) }

$program = $doc.program
if (-not $program) { Fail "program is missing" }
elseif (-not $program.name) { Fail "program.name is missing" }
elseif ($program.name.Length -lt 10 -or $program.name.Length -gt 70) { Fail "program.name should be 10-70 characters (readiness check 'title')" }
if (-not $program.providerCode) { Fail "program.providerCode is missing" }
if (-not $doc.destination -or -not $doc.destination.name -or -not $doc.destination.country) { Fail "destination.name and destination.country are required" }
elseif ($doc.destination.country.Length -ne 2) { Fail "destination.country must be an ISO 3166-1 alpha-2 code" }
if (-not $doc.hotels -or @($doc.hotels).Count -eq 0) { Fail "at least one hotel is required" }
if (-not $doc.departures -or @($doc.departures).Count -eq 0) { Fail "at least one departure is required" }
$invariant = [Globalization.CultureInfo]::InvariantCulture
$noStyle = [Globalization.DateTimeStyles]::None
foreach ($d in @($doc.departures)) {
    [DateTime] $start = [DateTime]::MinValue
    [DateTime] $end = [DateTime]::MinValue
    if (-not [DateTime]::TryParseExact([string]$d.startDate, 'yyyy-MM-dd', $invariant, $noStyle, [ref]$start)) { Fail "departure startDate '$($d.startDate)' is not yyyy-MM-dd" }
    if (-not [DateTime]::TryParseExact([string]$d.endDate, 'yyyy-MM-dd', $invariant, $noStyle, [ref]$end)) { Fail "departure endDate '$($d.endDate)' is not yyyy-MM-dd" }
    if ($start -ne [DateTime]::MinValue -and $end -ne [DateTime]::MinValue -and $end -le $start) { Fail "departure $($d.startDate): endDate must be after startDate" }
}
$content = $program.content
if ($content) {
    if ($content.summary -and $content.summary.Length -lt 50) { Fail "content.summary should be at least 50 characters" }
    if (-not $content.cancellationPolicy) { Fail "content.cancellationPolicy is required to publish" }
    if (-not $content.inclusions -or @($content.inclusions).Count -eq 0) { Fail "content.inclusions needs at least one item" }
    if ($content.slug -and $content.slug -notmatch '^[a-z0-9]+(-[a-z0-9]+)*$') { Fail "content.slug must be lowercase kebab-case" }
    $days = @($content.itinerary)
    for ($i = 0; $i -lt $days.Count; $i++) {
        if ([int]$days[$i].day -ne ($i + 1)) { Fail "itinerary days must run 1, 2, 3... in order (found $($days[$i].day) at position $($i + 1))"; break }
    }
}
else { Fail "program.content is missing (the program would fail every content check)" }

$images = @($doc.images)
$covers = @($images | Where-Object { $_.isCover }).Count
if ($covers -gt 1) { Fail "only one image can be the cover" }
foreach ($img in $images) {
    $path = if ([IO.Path]::IsPathRooted($img.file)) { $img.file } else { Join-Path $specDir $img.file }
    if (-not (Test-Path $path)) { Fail "image not found: $path"; continue }
    $file = Get-Item $path
    if ($file.Length -gt 5MB) { Fail "image over 5 MB: $path" }
    if ($file.Extension -notin '.jpg', '.jpeg', '.png', '.webp') { Fail "image must be JPEG, PNG or WebP: $path" }
    if (-not $img.altText) { Fail "image without altText: $($img.file) (readiness check 'alt-text')" }
    $img | Add-Member -NotePropertyName resolvedPath -NotePropertyValue $file.FullName -Force
}

Write-Host ""
Write-Host "Plan for '$($program.name)'" -ForegroundColor Cyan
Write-Host "  destination : $($doc.destination.name), $($doc.destination.country)"
Write-Host "  provider    : $($program.providerCode)"
Write-Host "  departures  : $(@($doc.departures).Count)  $(( @($doc.departures) | ForEach-Object { "$($_.startDate)..$($_.endDate)" }) -join ', ')"
Write-Host "  hotels      : $(( @($doc.hotels) | ForEach-Object { $_.name }) -join ', ')"
Write-Host "  itinerary   : $(@($content.itinerary).Count) days"
Write-Host "  images      : $($images.Count)"
Write-Host "  publish     : $([bool]$doc.publish -or $Publish)"

if ($problems.Count -gt 0) {
    Write-Host ""
    Write-Host "Spec problems:" -ForegroundColor Red
    $problems | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
if ($DryRun) {
    Write-Host ""
    Write-Host "Dry run: the spec is valid. Nothing was sent." -ForegroundColor Green
    exit 0
}

# ---------- API helpers ----------
if (-not $Token) {
    if (-not $Email -or -not $Password) { throw "Pass -Token, or -Email and -Password to log in." }
    $loginBody = [Text.Encoding]::UTF8.GetBytes((@{ email = $Email; password = $Password } | ConvertTo-Json))
    $login = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/auth/login" -ContentType 'application/json; charset=utf-8' -Body $loginBody
    $Token = $login.accessToken
}
$headers = @{ Authorization = "Bearer $Token" }

function Invoke-Api {
    param([string] $Method, [string] $Path, $Body, [int[]] $Tolerate = @())
    $uri = "$BaseUrl$Path"
    try {
        if ($null -ne $Body) {
            $json = $Body | ConvertTo-Json -Depth 20
            $bytes = [Text.Encoding]::UTF8.GetBytes($json)
            $result = Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType 'application/json; charset=utf-8' -Body $bytes
        }
        else {
            $result = Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
        }
        # Windows PowerShell 5.1 hands a JSON array back as one object; emit the items so @() flattens it.
        if ($result -is [System.Collections.IList]) { foreach ($item in $result) { $item } }
        else { $result }
        return
    }
    catch {
        $status = 0
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        if ($Tolerate -contains $status) { return $null }
        $detail = $_.ErrorDetails.Message
        if (-not $detail -and $_.Exception.Response -and $_.Exception.Response.GetResponseStream) {
            $reader = New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())
            $detail = $reader.ReadToEnd()
        }
        throw "$Method $Path failed ($status): $detail"
    }
}

# ---------- destination ----------
$destinations = @(Invoke-Api GET "/api/v1/destinations")
$destination = $destinations | Where-Object { $_.name -ieq $doc.destination.name -and $_.country -ieq $doc.destination.country } | Select-Object -First 1
if ($destination) { Write-Host "destination : found $($destination.name) ($($destination.id))" }
else {
    $destination = Invoke-Api POST "/api/v1/destinations" @{ name = $doc.destination.name; country = $doc.destination.country.ToUpper() }
    Write-Host "destination : created $($destination.name) ($($destination.id))" -ForegroundColor Yellow
}
if ($doc.destination.providerLocationCode) {
    $mappings = @(Invoke-Api GET "/api/v1/destinations/$($destination.id)/mappings")
    if (-not ($mappings | Where-Object { $_.providerCode -ieq $program.providerCode })) {
        Invoke-Api POST "/api/v1/destinations/$($destination.id)/mappings" @{ providerCode = $program.providerCode; externalLocationCode = $doc.destination.providerLocationCode } | Out-Null
        Write-Host "destination : mapped to $($program.providerCode) as $($doc.destination.providerLocationCode)" -ForegroundColor Yellow
    }
}

# ---------- hotels ----------
$existingHotels = @(Invoke-Api GET "/api/v1/hotels?destinationId=$($destination.id)")
$hotelIds = @()
foreach ($h in @($doc.hotels)) {
    $hotel = $existingHotels | Where-Object { $_.name -ieq $h.name } | Select-Object -First 1
    if ($hotel) { Write-Host "hotel       : found $($hotel.name)" }
    else {
        $stars = if ($h.stars) { [int]$h.stars } else { 3 }
        $hotel = Invoke-Api POST "/api/v1/hotels" @{ name = $h.name; destinationId = $destination.id; stars = $stars }
        Write-Host "hotel       : created $($hotel.name) ($stars*)" -ForegroundColor Yellow
    }
    if ($h.providerHotelCode) {
        $mappings = @(Invoke-Api GET "/api/v1/hotels/$($hotel.id)/mappings")
        if (-not ($mappings | Where-Object { $_.providerCode -ieq $program.providerCode })) {
            Invoke-Api POST "/api/v1/hotels/$($hotel.id)/mappings" @{ providerCode = $program.providerCode; externalHotelCode = $h.providerHotelCode } | Out-Null
            Write-Host "hotel       : mapped $($hotel.name) to $($program.providerCode) as $($h.providerHotelCode)" -ForegroundColor Yellow
        }
    }
    $hotelIds += $hotel.id
}

# ---------- program ----------
$createBody = @{
    name          = $program.name
    destinationId = $destination.id
    providerCode  = $program.providerCode
    season        = $program.season
    description   = $program.description
    content       = $content
}
$created = Invoke-Api POST "/api/v1/programs" $createBody
Write-Host "program     : created $($created.name) -> $($created.id) (slug $($created.slug))" -ForegroundColor Green

foreach ($d in @($doc.departures)) {
    Invoke-Api POST "/api/v1/programs/$($created.id)/departures" @{ startDate = $d.startDate; endDate = $d.endDate; transportNote = $d.transportNote } | Out-Null
}
Write-Host "departures  : $(@($doc.departures).Count) added"

foreach ($hotelId in $hotelIds) {
    Invoke-Api POST "/api/v1/programs/$($created.id)/hotels/$hotelId" -Tolerate 409 | Out-Null
}
Write-Host "hotels      : $($hotelIds.Count) attached"

# ---------- images (multipart via curl.exe) ----------
$curl = Get-Command curl.exe -ErrorAction SilentlyContinue
if ($images.Count -gt 0 -and -not $curl) { throw "curl.exe is needed for image uploads and was not found on PATH." }
foreach ($img in $images) {
    $args = @('-sS', '-f', '-X', 'POST', "$BaseUrl/api/v1/programs/$($created.id)/images",
              '-H', "Authorization: Bearer $Token",
              '-F', "file=@$($img.resolvedPath)",
              '-F', "isCover=$(if ($img.isCover) { 'true' } else { 'false' })")
    if ($img.caption) { $args += @('-F', "caption=$($img.caption)") }
    if ($img.altText) { $args += @('-F', "altText=$($img.altText)") }
    $response = & $curl.Source @args
    if ($LASTEXITCODE -ne 0) { throw "image upload failed for $($img.file): $response" }
    $uploaded = $response | ConvertFrom-Json
    Write-Host "image       : $($img.file) -> $($uploaded.width)x$($uploaded.height)$(if ($uploaded.isCover) { ' (cover)' })"
}

# ---------- readiness ----------
$readiness = Invoke-Api GET "/api/v1/programs/$($created.id)/readiness"
Write-Host ""
Write-Host "Readiness: $($readiness.score)/100, can publish: $($readiness.canPublish)" -ForegroundColor Cyan
foreach ($check in $readiness.checks | Where-Object { -not $_.passed }) {
    $kind = if ($check.required) { 'REQUIRED' } else { 'recommended' }
    Write-Host "  [$kind] $($check.title): $($check.detail)" -ForegroundColor $(if ($check.required) { 'Red' } else { 'Yellow' })
}

if ($Publish -or [bool]$doc.publish) {
    $published = Invoke-Api POST "/api/v1/programs/$($created.id)/publish"
    Write-Host "program     : status $($published.status)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Program id : $($created.id)"
Write-Host "Slug       : $($created.slug)"
Write-Host "Operator   : $BaseUrl/api/v1/programs/$($created.id)"
