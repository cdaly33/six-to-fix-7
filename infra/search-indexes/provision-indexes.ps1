<#
.SYNOPSIS
    Provisions the Azure AI Search index for Six-to-Fix.

.DESCRIPTION
    Creates the following index if it does not already exist:
      - six-to-fix-evidence          (evidence retrieval — pre-audit; semantic + vector search)

    Removed indexes (data lives in PostgreSQL — no AI Search copy needed):
      - six-to-fix-skill-outputs     (removed; data in skill_runs table)
      - six-to-fix-calibration       (removed; data in calibration_deltas table)

    Index is created via the Azure AI Search REST API using az rest.
    Existing index is left unchanged (idempotent — safe to re-run).

.PARAMETER SearchServiceName
    The name of your Azure AI Search resource (just the name, not the full URL).
    Example: six-to-fix-search-dev

.PARAMETER SubscriptionId
    Your Azure subscription ID. If omitted, uses the current az account.

.EXAMPLE
    .\provision-indexes.ps1 -SearchServiceName six-to-fix-search-dev

.NOTES
    Prerequisites:
      - Azure CLI installed and logged in (az login)
      - Contributor or Search Service Contributor role on the Search resource
      - .NET 10 solution already built (not strictly required for this script)
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$SearchServiceName,

    [Parameter(Mandatory = $false)]
    [string]$SubscriptionId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Resolve subscription ---
if (-not $SubscriptionId) {
    $SubscriptionId = (az account show --query id -o tsv)
    if (-not $SubscriptionId) {
        Write-Error "Not logged in to Azure. Run 'az login' first."
        exit 1
    }
}

$BaseUrl = "https://$SearchServiceName.search.windows.net"
$ApiVersion = "2024-07-01"

Write-Host ""
Write-Host "Search service : $SearchServiceName" -ForegroundColor Cyan
Write-Host "Endpoint       : $BaseUrl" -ForegroundColor Cyan
Write-Host "API version    : $ApiVersion" -ForegroundColor Cyan
Write-Host ""

# Helper: create or skip an index
function Invoke-CreateIndex {
    param(
        [string]$IndexName,
        [object]$Schema
    )

    Write-Host "Checking index '$IndexName'..." -NoNewline

    # Check if it already exists
    $checkUrl = "$BaseUrl/indexes/$IndexName`?api-version=$ApiVersion"
    try {
        $null = az rest --method GET --url $checkUrl --resource "https://search.azure.com" 2>&1
        Write-Host " already exists, skipping." -ForegroundColor Yellow
        return
    }
    catch {
        # 404 = does not exist — proceed to create
    }

    Write-Host " creating..." -NoNewline

    $body = $Schema | ConvertTo-Json -Depth 20 -Compress
    $tempFile = [System.IO.Path]::GetTempFileName() + ".json"
    $body | Set-Content -Path $tempFile -Encoding UTF8

    $createUrl = "$BaseUrl/indexes`?api-version=$ApiVersion"
    az rest --method POST --url $createUrl --resource "https://search.azure.com" `
        --headers "Content-Type=application/json" `
        --body "@$tempFile" | Out-Null

    Remove-Item $tempFile -Force
    Write-Host " done." -ForegroundColor Green
}

# ----------------------------------------------------------------
# Index 1: six-to-fix-evidence
# ----------------------------------------------------------------
$evidenceIndex = @{
    name   = "six-to-fix-evidence"
    fields = @(
        @{ name = "id";            type = "Edm.String";              key = $true;  filterable = $true }
        @{ name = "tenantId";      type = "Edm.String";              filterable = $true;  sortable = $false; facetable = $false }
        @{ name = "clientId";      type = "Edm.String";              filterable = $true }
        @{ name = "documentId";    type = "Edm.String";              filterable = $true }
        @{ name = "area";          type = "Edm.String";              filterable = $true;  facetable = $true }
        @{ name = "content";       type = "Edm.String";              searchable = $true;  analyzer = "en.microsoft" }
        @{ name = "contentVector"; type = "Collection(Edm.Single)";  searchable = $true;  dimensions = 1536; vectorSearchProfile = "hnsw-profile" }
        @{ name = "documentTitle"; type = "Edm.String";              searchable = $true }
        @{ name = "chunkIndex";    type = "Edm.Int32";               filterable = $true;  sortable = $true }
        @{ name = "uploadedAt";    type = "Edm.DateTimeOffset";      filterable = $true;  sortable = $true }
    )
    vectorSearch = @{
        profiles   = @(@{ name = "hnsw-profile"; algorithm = "hnsw-config" })
        algorithms = @(@{
            name           = "hnsw-config"
            kind           = "hnsw"
            hnswParameters = @{ m = 4; efConstruction = 400; efSearch = 500; metric = "cosine" }
        })
    }
    semantic = @{
        defaultConfiguration = "semantic-config"
        configurations       = @(@{
            name              = "semantic-config"
            prioritizedFields = @{
                contentFields  = @(@{ fieldName = "content" })
                keywordsFields = @(@{ fieldName = "documentTitle" })
            }
        })
    }
}

# ----------------------------------------------------------------
# Provision the evidence index
# ----------------------------------------------------------------
Invoke-CreateIndex -IndexName "six-to-fix-evidence" -Schema $evidenceIndex

Write-Host ""
Write-Host "Index provisioned successfully." -ForegroundColor Green
Write-Host ""
Write-Host "Next: ensure the App Service managed identity has these roles on '$SearchServiceName':"
Write-Host "  - Search Index Data Contributor  (for indexing documents)"
Write-Host "  - Search Index Data Reader        (for search queries)"
Write-Host ""
Write-Host "Grant command:"
Write-Host "  az role assignment create --assignee <managed-identity-principal-id> \"
Write-Host "    --role 'Search Index Data Contributor' \"
Write-Host "    --scope /subscriptions/$SubscriptionId/resourceGroups/rg-StrategicGlue-CommandCenter/providers/Microsoft.Search/searchServices/$SearchServiceName"
