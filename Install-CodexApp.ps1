[CmdletBinding()]
param(
    [string]$TargetDir = "",
    [string]$Ring = "RP",
    [switch]$ResolveOnly,
    [switch]$KeepWorkDir
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$CategoryId = "fdf7dba1-a7bc-4592-ad8e-04aa3b974675"
$PackageRegex = "OpenAI\.Codex_(?<version>[0-9][0-9.]*?)_x64__2p2nqsd0c76g0(?:\.msix)?$"
$WuUrl = "https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx"
$WuSecureUrl = "https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx/secured"
$DeviceToken = @'
dAA9AEUAdwBBAHcAQQBzAE4AMwBCAEEAQQBVADEAYgB5AHMAZQBtAGIAZQBEAFYAQwArADMAZgBtADcAbwBXAHkASAA3AGIAbgBnAEcAWQBtAEEAQQBMAGoAbQBqAFYAVQB2AFEAYwA0AEsAVwBFAC8AYwBDAEwANQBYAGUANABnAHYAWABkAGkAegBHAGwAZABjADEAZAAvAFcAeQAvAHgASgBQAG4AVwBRAGUAYwBtAHYAbwBjAGkAZwA5AGoAZABwAE4AawBIAG0AYQBzAHAAVABKAEwARAArAFAAYwBBAFgAbQAvAFQAcAA3AEgAagBzAEYANAA0AEgAdABsAC8AMQBtAHUAcgAwAFMAdQBtAG8AMABZAGEAdgBqAFIANwArADQAcABoAC8AcwA4ADEANgBFAFkANQBNAFIAbQBnAFIAQwA2ADMAQwBSAEoAQQBVAHYAZgBzADQAaQB2AHgAYwB5AEwAbAA2AHoAOABlAHgAMABrAFgAOQBPAHcAYQB0ADEAdQBwAFMAOAAxAEgANgA4AEEASABzAEoAegBnAFQAQQBMAG8AbgBBADIAWQBBAEEAQQBpAGcANQBJADMAUQAvAFYASABLAHcANABBAEIAcQA5AFMAcQBhADEAQgA4AGsAVQAxAGEAbwBLAEEAdQA0AHYAbABWAG4AdwBWADMAUQB6AHMATgBtAEQAaQBqAGgANQBkAEcAcgBpADgAQQBlAEUARQBWAEcAbQBXAGgASQBCAE0AUAAyAEQAVwA0ADMAZABWAGkARABUAHoAVQB0AHQARQBMAEgAaABSAGYAcgBhAGIAWgBsAHQAQQBUAEUATABmAHMARQBGAFUAYQBRAFMASgB4ADUAeQBRADgAagBaAEUAZQAyAHgANABCADMAMQB2AEIAMgBqAC8AUgBLAGEAWQAvAHEAeQB0AHoANwBUAHYAdAB3AHQAagBzADYAUQBYAEIAZQA4AHMAZwBJAG8AOQBiADUAQQBCADcAOAAxAHMANgAvAGQAUwBFAHgATgBEAEQAYQBRAHoAQQBYAFAAWABCAFkAdQBYAFEARQBzAE8AegA4AHQAcgBpAGUATQBiAEIAZQBUAFkAOQBiAG8AQgBOAE8AaQBVADcATgBSAEYAOQAzAG8AVgArAFYAQQBiAGgAcAAwAHAAUgBQAFMAZQBmAEcARwBPAHEAdwBTAGcANwA3AHMAaAA5AEoASABNAHAARABNAFMAbgBrAHEAcgAyAGYARgBpAEMAUABrAHcAVgBvAHgANgBuAG4AeABGAEQAbwBXAC8AYQAxAHQAYQBaAHcAegB5AGwATABMADEAMgB3AHUAYgBtADUAdQBtAHAAcQB5AFcAYwBLAFIAagB5AGgAMgBKAFQARgBKAFcANQBnAFgARQBJADUAcAA4ADAARwB1ADIAbgB4AEwAUgBOAHcAaQB3AHIANwBXAE0AUgBBAFYASwBGAFcATQBlAFIAegBsADkAVQBxAGcALwBwAFgALwB2AGUATAB3AFMAawAyAFMAUwBIAGYAYQBLADYAagBhAG8AWQB1AG4AUgBHAHIAOABtAGIARQBvAEgAbABGADYASgBDAGEAYQBUAEIAWABCAGMAdgB1AGUAQwBKAG8AOQA4AGgAUgBBAHIARwB3ADQAKwBQAEgAZQBUAGIATgBTAEUAWABYAHoAdgBaADYAdQBXADUARQBBAGYAZABaAG0AUwA4ADgAVgBKAGMAWgBhAEYASwA3AHgAeABnADAAdwBvAG4ANwBoADAAeABDADYAWgBCADAAYwBZAGoATAByAC8ARwBlAE8AegA5AEcANABRAFUASAA5AEUAawB5ADAAZAB5AEYALwByAGUAVQAxAEkAeQBpAGEAcABwAGgATwBQADgAUwAyAHQANABCAHIAUABaAFgAVAB2AEMAMABQADcAegBPACsAZgBHAGsAeABWAG0AKwBVAGYAWgBiAFEANQA1AHMAdwBFAD0AJgBwAD0A
'@.Trim()

$DisplayWidth = 64

function Require-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name was not found in PATH."
    }
}

function Write-Rule {
    Write-Host ("=" * $DisplayWidth) -ForegroundColor DarkCyan
}

function Write-Banner {
    param([string]$TargetPath, [bool]$ResolveOnlyMode)

    try { [Console]::Title = "Codex App Installer" } catch {}

    Write-Host ""
    Write-Rule
    Write-Host " Codex App Installer" -ForegroundColor White
    Write-Host " Fresh Microsoft Store URL every run. No local cache." -ForegroundColor DarkGray
    Write-Rule
    if ($TargetPath) { Write-Info "Target" $TargetPath }
    if ($ResolveOnlyMode) {
        Write-Info "Mode" "Resolve only"
    } else {
        Write-Info "Mode" "Download, extract, install"
    }
}

function Write-Step {
    param([int]$Index, [int]$Total, [string]$Text)

    Write-Host ""
    Write-Host ("[{0}/{1}] " -f $Index, $Total) -NoNewline -ForegroundColor DarkCyan
    Write-Host $Text -ForegroundColor White
}

function Format-Duration {
    param([TimeSpan]$Elapsed)

    if ($Elapsed.TotalSeconds -lt 1) { return ("{0:N0}ms" -f $Elapsed.TotalMilliseconds) }
    "{0:N1}s" -f $Elapsed.TotalSeconds
}

function Invoke-SubStep {
    param([string]$Text, [scriptblock]$Action)

    Write-Host ("   - {0,-46}" -f $Text) -NoNewline -ForegroundColor DarkGray
    $sw = [Diagnostics.Stopwatch]::StartNew()
    try {
        $result = & $Action
        $sw.Stop()
        Write-Host (" OK   {0,7}" -f (Format-Duration $sw.Elapsed)) -ForegroundColor Green
        return $result
    } catch {
        $sw.Stop()
        Write-Host (" FAIL {0,7}" -f (Format-Duration $sw.Elapsed)) -ForegroundColor Red
        throw
    }
}

function Write-Info {
    param([string]$Label, [string]$Value)
    Write-Host ("   {0,-12} {1}" -f ($Label + ":"), $Value)
}

function Write-Ok {
    param([string]$Text)
    Write-Host ("   OK          " + $Text) -ForegroundColor Green
}

function Write-Complete {
    param([string]$TargetPath, [string]$PackageName)

    Write-Host ""
    Write-Rule
    Write-Host " Installed successfully" -ForegroundColor Green
    Write-Rule
    Write-Info "Install" $TargetPath
    Write-Info "Package" $PackageName
}

function Write-Failure {
    param([string]$Message)

    Write-Host ""
    Write-Rule
    Write-Host " Install failed" -ForegroundColor Red
    Write-Rule
    Write-Info "Error" $Message
}

function Invoke-CurlText {
    param(
        [string]$Uri,
        [string]$Body = "",
        [string]$ContentType = "",
        [int]$TimeoutSeconds = 120
    )

    $bodyFile = $null
    try {
        $args = @(
            "--ssl-no-revoke",
            "-L",
            "--silent",
            "--show-error",
            "--connect-timeout", "20",
            "--max-time", [string]$TimeoutSeconds,
            "--retry", "2",
            "--retry-delay", "2",
            "-w", "`n__HTTP_STATUS__:%{http_code}"
        )
        if ($ContentType) {
            $args += @("-H", "Content-Type: $ContentType")
        }
        if ($Body) {
            $bodyFile = [IO.Path]::GetTempFileName()
            [IO.File]::WriteAllText($bodyFile, $Body, [Text.Encoding]::ASCII)
            $args += @("--data-binary", "@$bodyFile")
        }
        $args += $Uri

        $output = & curl.exe @args 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "curl.exe failed with exit code $LASTEXITCODE. $($output | Out-String)"
        }

        $text = ($output | Out-String)
        $marker = "__HTTP_STATUS__:"
        $index = $text.LastIndexOf($marker)
        if ($index -lt 0) {
            throw "curl.exe did not return an HTTP status marker."
        }

        $bodyText = $text.Substring(0, $index).TrimEnd("`r", "`n")
        $statusCode = [int]$text.Substring($index + $marker.Length).Trim()
        if ($statusCode -lt 200 -or $statusCode -ge 300) {
            throw "HTTP $statusCode from $Uri. $bodyText"
        }

        $bodyText
    } finally {
        if ($bodyFile -and (Test-Path -LiteralPath $bodyFile)) {
            Remove-Item -LiteralPath $bodyFile -Force -ErrorAction SilentlyContinue
        }
    }
}

function Write-DownloadProgress {
    param([int64]$Downloaded, [int64]$Total, [switch]$Done)

    $innerWidth = [Math]::Max(10, $DisplayWidth - 2)
    if ($Total -gt 0) {
        $percent = [Math]::Min(100, ($Downloaded / $Total) * 100)
        $filled = [int][Math]::Floor(($percent / 100) * $innerWidth)
        if ($filled -gt $innerWidth) { $filled = $innerWidth }
        $bar = ("#" * $filled) + ("-" * ($innerWidth - $filled))
        $line = "   [{0}] {1,6:N1}%" -f $bar, $percent
    } else {
        $filled = [int](($Downloaded / 1MB) % ($innerWidth + 1))
        $bar = ("#" * $filled) + ("-" * ($innerWidth - $filled))
        $line = "   [{0}] {1,10}" -f $bar, (Format-Size $Downloaded)
    }

    Write-Host ("`r" + $line.PadRight($DisplayWidth + 14)) -NoNewline
    if ($Done) { Write-Host "" }
}

function Save-Url {
    param([string]$Uri, [string]$OutFile, [int64]$ExpectedBytes = 0)

    $buffer = New-Object byte[] (1024 * 1024)
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $response = $null
        $responseStream = $null
        $outputStream = $null

        try {
            if ($attempt -gt 1) {
                Write-Host ("   Retry {0}/3" -f $attempt) -ForegroundColor Yellow
            }

            $request = [Net.HttpWebRequest]::Create($Uri)
            $request.AllowAutoRedirect = $true
            $request.Timeout = 30000
            $request.ReadWriteTimeout = 30000
            $request.UserAgent = "CodexAppInstaller/1.0"

            $response = $request.GetResponse()
            $total = [int64]$response.ContentLength
            if ($total -le 0) { $total = $ExpectedBytes }

            $responseStream = $response.GetResponseStream()
            $outputStream = [IO.File]::Open($OutFile, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
            $downloaded = [int64]0
            $lastUpdate = [DateTime]::MinValue

            Write-DownloadProgress -Downloaded $downloaded -Total $total
            while (($read = $responseStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $outputStream.Write($buffer, 0, $read)
                $downloaded += $read

                $now = Get-Date
                if (($now - $lastUpdate).TotalMilliseconds -ge 120 -or ($total -gt 0 -and $downloaded -ge $total)) {
                    Write-DownloadProgress -Downloaded $downloaded -Total $total
                    $lastUpdate = $now
                }
            }
            Write-DownloadProgress -Downloaded $downloaded -Total $total -Done
            return
        } catch {
            Write-Host ""
            if ($attempt -ge 3) {
                throw "Download failed. $($_.Exception.Message)"
            }
            Write-Host ("   Download error: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
            Start-Sleep -Seconds 2
        } finally {
            if ($outputStream) { $outputStream.Dispose() }
            if ($responseStream) { $responseStream.Dispose() }
            if ($response) { $response.Dispose() }
        }
    }
}

function SoapPost {
    param([string]$Uri, [string]$Xml)
    Invoke-CurlText -Uri $Uri -Body $Xml -ContentType "application/soap+xml; charset=utf-8" -TimeoutSeconds 60
}

function Nodes {
    param([xml]$Xml, [string]$Name)
    $Xml.SelectNodes("//*[local-name()='$Name']")
}

function Attr {
    param([System.Xml.XmlNode]$Node, [string]$Name)
    if (-not $Node -or -not $Node.Attributes) { return "" }
    $attr = $Node.Attributes.GetNamedItem($Name)
    if ($attr) { return $attr.Value }
    ""
}

function As-Version {
    param([string]$Text)
    if ($Text -match $PackageRegex) {
        return [version]$matches["version"]
    }
    [version]"0.0.0.0"
}

function Format-Size {
    param([double]$Bytes)
    if ($Bytes -lt 1KB) { return ("{0:N0} B" -f $Bytes) }
    if ($Bytes -lt 1MB) { return ("{0:N1} KB" -f ($Bytes / 1KB)) }
    if ($Bytes -lt 1GB) { return ("{0:N1} MB" -f ($Bytes / 1MB)) }
    "{0:N2} GB" -f ($Bytes / 1GB)
}

function HexToBase64 {
    param([string]$Hex)
    $bytes = New-Object byte[] ($Hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($Hex.Substring($i * 2, 2), 16)
    }
    [Convert]::ToBase64String($bytes)
}

function Get-DigestAlgorithm {
    param([string]$Digest)

    try {
        $bytes = [Convert]::FromBase64String($Digest)
    } catch {
        throw "The package digest is not valid Base64: $Digest"
    }

    switch ($bytes.Length) {
        20 { return "SHA1" }
        32 { return "SHA256" }
        default { throw "Unsupported package digest length: $($bytes.Length) bytes." }
    }
}

function Test-FileDigest {
    param([string]$Path, [string]$Digest)

    $algorithm = Get-DigestAlgorithm $Digest
    $actual = HexToBase64 (Get-FileHash -LiteralPath $Path -Algorithm $algorithm).Hash
    if ($actual -ne $Digest) {
        throw "$algorithm mismatch. Expected $Digest, got $actual."
    }
    Write-Ok "$algorithm verified"
}

function New-CookieSoap {
    $created = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    $expires = (Get-Date).ToUniversalTime().AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
@"
<Envelope xmlns="http://www.w3.org/2003/05/soap-envelope">
  <Header>
    <Action d3p1:mustUnderstand="1" xmlns:d3p1="http://www.w3.org/2003/05/soap-envelope" xmlns="http://www.w3.org/2005/08/addressing">http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService/GetCookie</Action>
    <MessageID xmlns="http://www.w3.org/2005/08/addressing">urn:uuid:b9b43757-2247-4d7b-ae8f-a71ba8a22386</MessageID>
    <To d3p1:mustUnderstand="1" xmlns:d3p1="http://www.w3.org/2003/05/soap-envelope" xmlns="http://www.w3.org/2005/08/addressing">$WuUrl</To>
    <Security d3p1:mustUnderstand="1" xmlns:d3p1="http://www.w3.org/2003/05/soap-envelope" xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
      <Timestamp xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"><Created>$created</Created><Expires>$expires</Expires></Timestamp>
      <WindowsUpdateTicketsToken d4p1:id="ClientMSA" xmlns:d4p1="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd" xmlns="http://schemas.microsoft.com/msus/2014/10/WindowsUpdateAuthorization">
        <TicketType Name="MSA" Version="1.0" Policy="MBI_SSL"><User /></TicketType>
      </WindowsUpdateTicketsToken>
    </Security>
  </Header>
  <Body>
    <GetCookie xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
      <oldCookie></oldCookie><lastChange>2015-10-21T17:01:07.1472913Z</lastChange><currentTime>$created</currentTime><protocolVersion>1.40</protocolVersion>
    </GetCookie>
  </Body>
</Envelope>
"@
}

function Get-WuCookie {
    [xml]$xml = Invoke-SubStep "1.1 Windows Update cookie" {
        SoapPost -Uri $WuUrl -Xml (New-CookieSoap)
    }
    $node = @(Nodes $xml "EncryptedData") | Select-Object -First 1
    if (-not $node.InnerText) { throw "GetCookie did not return EncryptedData." }
    $node.InnerText
}

function New-SyncSoap {
    param([string]$Cookie, [string]$CategoryId)
@"
<s:Envelope xmlns:a="http://www.w3.org/2005/08/addressing" xmlns:s="http://www.w3.org/2003/05/soap-envelope">
  <s:Header>
    <a:Action s:mustUnderstand="1">http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService/SyncUpdates</a:Action>
    <a:MessageID>urn:uuid:175df68c-4b91-41ee-b70b-f2208c65438e</a:MessageID>
    <a:To s:mustUnderstand="1">$WuUrl</a:To>
    <o:Security s:mustUnderstand="1" xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
      <wuws:WindowsUpdateTicketsToken wsu:id="ClientMSA" xmlns:wsu="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd" xmlns:wuws="http://schemas.microsoft.com/msus/2014/10/WindowsUpdateAuthorization">
        <TicketType Name="MSA" Version="1.0" Policy="MBI_SSL"><Device>$DeviceToken</Device></TicketType>
      </wuws:WindowsUpdateTicketsToken>
    </o:Security>
  </s:Header>
  <s:Body>
    <SyncUpdates xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
      <cookie><Expiration>2045-03-11T02:02:48Z</Expiration><EncryptedData>$Cookie</EncryptedData></cookie>
      <parameters>
        <ExpressQuery>false</ExpressQuery>
        <InstalledNonLeafUpdateIDs>
          <int>1</int><int>2</int><int>3</int><int>11</int><int>19</int><int>544</int><int>549</int><int>2359974</int><int>2359977</int><int>5169044</int><int>8788830</int><int>23110993</int><int>23110994</int><int>54341900</int><int>54343656</int><int>59830006</int><int>59830007</int><int>59830008</int><int>60484010</int><int>62450018</int><int>62450019</int><int>62450020</int><int>66027979</int><int>66053150</int><int>97657898</int><int>98822896</int><int>98959022</int><int>98959023</int><int>98959024</int><int>98959025</int><int>98959026</int><int>104433538</int><int>104900364</int><int>105489019</int><int>117765322</int><int>129905029</int><int>130040031</int><int>132387090</int><int>132393049</int><int>133399034</int><int>138537048</int><int>140377312</int><int>143747671</int><int>158941041</int><int>158941042</int><int>158941043</int><int>158941044</int><int>159123858</int><int>159130928</int><int>164836897</int><int>164847386</int><int>164848327</int><int>164852241</int><int>164852246</int><int>164852252</int><int>164852253</int>
        </InstalledNonLeafUpdateIDs>
        <OtherCachedUpdateIDs></OtherCachedUpdateIDs>
        <SkipSoftwareSync>false</SkipSoftwareSync>
        <NeedTwoGroupOutOfScopeUpdates>true</NeedTwoGroupOutOfScopeUpdates>
        <FilterAppCategoryIds><CategoryIdentifier><Id>$CategoryId</Id></CategoryIdentifier></FilterAppCategoryIds>
        <TreatAppCategoryIdsAsInstalled>true</TreatAppCategoryIdsAsInstalled>
        <AlsoPerformRegularSync>false</AlsoPerformRegularSync>
        <ComputerSpec />
        <ExtendedUpdateInfoParameters><XmlUpdateFragmentTypes><XmlUpdateFragmentType>Extended</XmlUpdateFragmentType></XmlUpdateFragmentTypes></ExtendedUpdateInfoParameters>
        <ProductsParameters>
          <SyncCurrentVersionOnly>false</SyncCurrentVersionOnly>
          <DeviceAttributes>FlightRing=$Ring;DeviceFamily=Windows.Desktop;</DeviceAttributes>
          <CallerAttributes>Interactive=1;IsSeeker=0;</CallerAttributes>
          <Products />
        </ProductsParameters>
      </parameters>
    </SyncUpdates>
  </s:Body>
</s:Envelope>
"@
}

function Get-WuUpdates {
    param([string]$Cookie, [string]$CategoryId)

    $response = Invoke-SubStep "1.2 Package metadata" {
        SoapPost -Uri $WuUrl -Xml (New-SyncSoap -Cookie $Cookie -CategoryId $CategoryId)
    }
    [xml]$xml = [Net.WebUtility]::HtmlDecode($response)

    $files = @{}
    foreach ($file in Nodes $xml "File") {
        $id = Attr $file "InstallerSpecificIdentifier"
        if (-not $id) { continue }
        $sizeBytes = [int64](Attr $file "Size")
        $files[$id] = [pscustomobject]@{
            Extension = [IO.Path]::GetExtension((Attr $file "FileName"))
            Digest = Attr $file "Digest"
            SizeBytes = $sizeBytes
            Size = Format-Size $sizeBytes
        }
    }

    foreach ($info in Nodes $xml "UpdateInfo") {
        $meta = $info.SelectSingleNode(".//*[local-name()='AppxMetadata']")
        $moniker = Attr $meta "PackageMoniker"
        if (-not $moniker -or -not $files.ContainsKey($moniker)) { continue }

        $identity = $info.SelectSingleNode(".//*[local-name()='UpdateIdentity']")
        [pscustomobject]@{
            Name = $moniker + $files[$moniker].Extension
            Version = As-Version ($moniker + $files[$moniker].Extension)
            Digest = $files[$moniker].Digest
            SizeBytes = $files[$moniker].SizeBytes
            Size = $files[$moniker].Size
            UpdateId = Attr $identity "UpdateID"
            Revision = Attr $identity "RevisionNumber"
        }
    }
}

function New-UrlSoap {
    param([string]$UpdateId, [string]$Revision)
@"
<s:Envelope xmlns:a="http://www.w3.org/2005/08/addressing" xmlns:s="http://www.w3.org/2003/05/soap-envelope">
  <s:Header>
    <a:Action s:mustUnderstand="1">http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService/GetExtendedUpdateInfo2</a:Action>
    <a:MessageID>urn:uuid:2cc99c2e-3b3e-4fb1-9e31-0cd30e6f43a0</a:MessageID>
    <a:To s:mustUnderstand="1">$WuSecureUrl</a:To>
    <o:Security s:mustUnderstand="1" xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
      <wuws:WindowsUpdateTicketsToken wsu:id="ClientMSA" xmlns:wsu="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd" xmlns:wuws="http://schemas.microsoft.com/msus/2014/10/WindowsUpdateAuthorization">
        <TicketType Name="MSA" Version="1.0" Policy="MBI_SSL"><Device>$DeviceToken</Device></TicketType>
      </wuws:WindowsUpdateTicketsToken>
    </o:Security>
  </s:Header>
  <s:Body>
    <GetExtendedUpdateInfo2 xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
      <updateIDs><UpdateIdentity><UpdateID>$UpdateId</UpdateID><RevisionNumber>$Revision</RevisionNumber></UpdateIdentity></updateIDs>
      <infoTypes><XmlUpdateFragmentType>FileUrl</XmlUpdateFragmentType><XmlUpdateFragmentType>FileDecryption</XmlUpdateFragmentType></infoTypes>
      <deviceAttributes>FlightRing=$Ring;DeviceFamily=Windows.Desktop;</deviceAttributes>
    </GetExtendedUpdateInfo2>
  </s:Body>
</s:Envelope>
"@
}

function Get-DownloadUrl {
    param($Update)

    [xml]$xml = Invoke-SubStep "1.3 Temporary download URL" {
        SoapPost -Uri $WuSecureUrl -Xml (New-UrlSoap -UpdateId $Update.UpdateId -Revision $Update.Revision)
    }
    $fallback = ""
    foreach ($location in Nodes $xml "FileLocation") {
        $digest = $location.SelectSingleNode(".//*[local-name()='FileDigest']")
        $url = $location.SelectSingleNode(".//*[local-name()='Url']")
        if (-not $url.InnerText) { continue }

        if (-not $fallback) { $fallback = $url.InnerText }
        if ($digest -and $digest.InnerText -eq $Update.Digest) { return $url.InnerText }
    }
    if ($fallback) { return $fallback }
    throw "GetExtendedUpdateInfo2 returned no download URL."
}

function Resolve-CodexPackage {
    $updates = @(Get-WuUpdates -Cookie (Get-WuCookie) -CategoryId $CategoryId)
    $update = $updates |
        Where-Object { $_.Name -match $PackageRegex } |
        Sort-Object Version -Descending |
        Select-Object -First 1

    if (-not $update) { throw "The Windows Update response did not include the Codex x64 msix." }

    [pscustomobject]@{
        FileName = $update.Name
        Url = Get-DownloadUrl $update
        Size = $update.Size
        SizeBytes = $update.SizeBytes
        Digest = $update.Digest
    }
}

function Resolve-TargetPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        $Path = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
    }
    $Path = [Environment]::ExpandEnvironmentVariables($Path.Trim())
    if (($Path.StartsWith('"') -and $Path.EndsWith('"')) -or ($Path.StartsWith("'") -and $Path.EndsWith("'"))) {
        $Path = $Path.Substring(1, $Path.Length - 2)
    }

    try {
        $full = [IO.Path]::GetFullPath($Path)
    } catch {
        throw "Invalid target path: $Path. $($_.Exception.Message)"
    }
    $root = [IO.Path]::GetPathRoot($full)
    if (-not (Test-Path -LiteralPath $root)) { throw "Target root does not exist: $root" }

    $parent = Split-Path -Path $full -Parent
    if ($parent -and -not (Test-Path -LiteralPath $parent)) {
        New-Item -Path $parent -ItemType Directory -Force | Out-Null
    }
    $full
}

function Expand-Msix {
    param([string]$MsixPath, [string]$Destination)

    & tar.exe -xf $MsixPath -C $Destination
    if ($LASTEXITCODE -eq 0) { return }

    Write-Warning "tar.exe failed with exit code $LASTEXITCODE; falling back to ZipFile."
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($MsixPath, $Destination)
}

function Find-AppDir {
    param([string]$ExtractDir)

    $rootApp = Join-Path $ExtractDir "app"
    if (Test-Path -LiteralPath $rootApp -PathType Container) {
        return (Get-Item -LiteralPath $rootApp).FullName
    }

    $app = Get-ChildItem -LiteralPath $ExtractDir -Directory -Recurse |
        Where-Object { $_.Name -ieq "app" } |
        Sort-Object FullName |
        Select-Object -First 1
    if (-not $app) { throw "The extracted msix did not contain an app directory." }
    $app.FullName
}

function Install-AppDir {
    param([string]$SourceDir, [string]$DestinationDir)

    New-Item -Path $DestinationDir -ItemType Directory -Force | Out-Null
    & robocopy.exe $SourceDir $DestinationDir /E /COPY:DAT /R:2 /W:2 /NFL /NDL /NP /NJH /NJS
    if ($LASTEXITCODE -ge 8) { throw "robocopy.exe failed with exit code $LASTEXITCODE." }
}

function Get-WorkDir {
    param([string]$TargetPath)

    Join-Path $TargetPath ("_codexmsix\" + (Get-Date -Format "yyyyMMdd-HHmmss"))
}

function Remove-Tree {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) { return }

    Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $Path) {
        cmd.exe /c rmdir /s /q "`"$Path`"" 2>$null | Out-Null
    }
}

try {
    Require-Command curl.exe
    Require-Command tar.exe
    Require-Command robocopy.exe

    if (-not $ResolveOnly) {
        $TargetDir = Resolve-TargetPath $TargetDir
    }

    $totalSteps = if ($ResolveOnly) { 1 } else { 5 }
    Write-Banner -TargetPath $TargetDir -ResolveOnlyMode ([bool]$ResolveOnly)

    Write-Step 1 $totalSteps "Resolve fresh Microsoft Store URL"
    $package = Resolve-CodexPackage
    Write-Ok "Package selected"
    Write-Info "File" $package.FileName
    Write-Info "Size" $package.Size

    if ($ResolveOnly) {
        Write-Host ""
        Write-Rule
        Write-Host " Resolved package" -ForegroundColor Green
        Write-Rule
        Write-Info "File" $package.FileName
        Write-Info "URL" $package.Url
        return
    }

    $workDir = Get-WorkDir $TargetDir
    $extractDir = Join-Path $workDir "x"
    $msixPath = Join-Path $workDir $package.FileName
    $keptWorkDir = ""
    $installed = $false

    New-Item -Path $extractDir -ItemType Directory -Force | Out-Null

    try {
        Write-Step 2 $totalSteps "Download MSIX"
        Save-Url -Uri $package.Url -OutFile $msixPath -ExpectedBytes $package.SizeBytes

        Write-Step 3 $totalSteps "Verify package hash"
        Test-FileDigest -Path $msixPath -Digest $package.Digest

        Write-Step 4 $totalSteps "Extract MSIX"
        Expand-Msix -MsixPath $msixPath -Destination $extractDir
        $appDir = Find-AppDir $extractDir

        Write-Step 5 $totalSteps "Install app files"
        Install-AppDir -SourceDir $appDir -DestinationDir $TargetDir
        $installed = $true
    } finally {
        if ($KeepWorkDir) {
            $keptWorkDir = $workDir
        } else {
            $workRoot = Split-Path -Path $workDir -Parent
            Remove-Tree $workDir
            if ((Test-Path -LiteralPath $workRoot) -and -not @(Get-ChildItem -LiteralPath $workRoot -Force -ErrorAction SilentlyContinue).Count) {
                Remove-Tree $workRoot
            }
        }
    }

    if ($installed) {
        Write-Complete -TargetPath $TargetDir -PackageName $package.FileName
        if ($keptWorkDir) { Write-Info "Work dir" $keptWorkDir }
    }
} catch {
    Write-Failure $_.Exception.Message
    exit 1
}
