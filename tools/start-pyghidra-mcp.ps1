param(
    [string]$Port = "8000",
    [string]$ProjectPath = "C:\Users\siki\gw2-re.gpr"
)

$env:GHIDRA_INSTALL_DIR = "C:\Program Files (x86)\Ghidra"
$env:Path = "C:\Users\siki\.local\bin;$env:Path"

$existing = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "pyghidra-mcp already listening on port $Port"
    exit 0
}

Write-Host "Starting pyghidra-mcp (streamable-http) on http://127.0.0.1:$Port/mcp ..."
uvx --python 3.13 pyghidra-mcp `
    --transport streamable-http `
    --host 127.0.0.1 `
    --port $Port `
    --project-path $ProjectPath