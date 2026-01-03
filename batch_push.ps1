
# Usage: ./batch_push.ps1
$maxBytes = 200 * 1024 * 1024 # 200 MB
$currentBatchSize = 0
$batchFiles = @()
$batchCount = 1

Write-Host "Unstaging all files..."
git restore --staged .

# Get lists of files
$modified = git ls-files --modified --others --exclude-standard
$totalFiles = $modified.Count
$processed = 0

foreach ($file in $modified) {
    if (Test-Path $file) {
        $item = Get-Item $file
        $size = $item.Length
        
        # Add to current batch
        $batchFiles += """$file"""
        $currentBatchSize += $size
        
        # If batch is full, commit and push
        if ($currentBatchSize -ge $maxBytes) {
            Write-Host "Batch $batchCount limit reached ($([math]::round($currentBatchSize / 1MB, 2)) MB). Processing..."
            
            git add $batchFiles
            git commit -m "Add: Assets Batch $batchCount (Texture/Terrain Data)"
            git push origin Terrain
            
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Push failed for batch $batchCount. Stopping."
                exit 1
            }
            
            # Reset for next batch
            $currentBatchSize = 0
            $batchFiles = @()
            $batchCount++
        }
    }
    $processed++
}

# Process remaining files
if ($batchFiles.Count -gt 0) {
    Write-Host "Processing final batch $batchCount ($([math]::round($currentBatchSize / 1MB, 2)) MB)..."
    git add $batchFiles
    git commit -m "Add: Final Assets Batch $batchCount"
    git push origin Terrain
}

Write-Host "Done! All batches pushed."
