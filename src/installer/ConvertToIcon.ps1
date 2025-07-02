# Convert PNG to ICO for installer using proper ICO format
param(
    [string]$PngPath = "C:\RevitAI\Icons\Tycoon Logo.png",
    [string]$IcoPath = "Resources\TycoonIcon.ico",
    [string]$LogoPngPath = "Resources\TycoonLogo.png"
)

Write-Host "Converting PNG to ICO and Logo PNG for installer..." -ForegroundColor Blue
Write-Host "Source: $PngPath" -ForegroundColor Gray
Write-Host "ICO Target: $IcoPath" -ForegroundColor Gray
Write-Host "Logo PNG Target: $LogoPngPath" -ForegroundColor Gray

try {
    # Load required assemblies
    Add-Type -AssemblyName System.Drawing

    # Check if source file exists
    if (!(Test-Path $PngPath)) {
        Write-Error "Source PNG file not found: $PngPath"
        exit 1
    }

    # Ensure target directory exists
    $targetDir = Split-Path $IcoPath -Parent
    if (!(Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }

    # Load the original PNG image
    $originalImage = [System.Drawing.Image]::FromFile($PngPath)
    Write-Host "Original image size: $($originalImage.Width)x$($originalImage.Height)" -ForegroundColor Green

    # Create multiple sizes for proper ICO format
    $sizes = @(16, 32, 48)
    $bitmaps = @()

    foreach ($size in $sizes) {
        Write-Host "Creating ${size}x${size} bitmap..." -ForegroundColor Gray
        $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

        # Set high quality rendering
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

        # Draw the resized image
        $graphics.DrawImage($originalImage, 0, 0, $size, $size)
        $graphics.Dispose()

        $bitmaps += $bitmap
    }

    # Create proper ICO file manually
    Write-Host "Creating ICO file with multiple sizes..." -ForegroundColor Gray

    # ICO file header (6 bytes)
    $iconDir = [byte[]]::new(6)
    $iconDir[0] = 0  # Reserved
    $iconDir[1] = 0  # Reserved
    $iconDir[2] = 1  # Type (1 = ICO)
    $iconDir[3] = 0  # Type high byte
    $iconDir[4] = $bitmaps.Count  # Number of images
    $iconDir[5] = 0  # Number high byte

    # Calculate directory entries and image data
    $directoryEntries = @()
    $imageData = @()
    $dataOffset = 6 + ($bitmaps.Count * 16)  # Header + directory entries

    for ($i = 0; $i -lt $bitmaps.Count; $i++) {
        $bitmap = $bitmaps[$i]
        $size = $bitmap.Width

        # Convert bitmap to PNG data for embedding
        $memoryStream = New-Object System.IO.MemoryStream
        $bitmap.Save($memoryStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngData = $memoryStream.ToArray()
        $memoryStream.Dispose()

        # Directory entry (16 bytes)
        $entry = [byte[]]::new(16)
        $entry[0] = if ($size -eq 256) { 0 } else { $size }  # Width
        $entry[1] = if ($size -eq 256) { 0 } else { $size }  # Height
        $entry[2] = 0  # Color count (0 for true color)
        $entry[3] = 0  # Reserved
        $entry[4] = 1  # Planes (low byte)
        $entry[5] = 0  # Planes (high byte)
        $entry[6] = 32  # Bits per pixel (low byte)
        $entry[7] = 0   # Bits per pixel (high byte)

        # Size of image data
        [BitConverter]::GetBytes([uint32]$pngData.Length).CopyTo($entry, 8)
        # Offset to image data
        [BitConverter]::GetBytes([uint32]$dataOffset).CopyTo($entry, 12)

        $directoryEntries += $entry
        $imageData += $pngData
        $dataOffset += $pngData.Length
    }

    # Write ICO file
    $fileStream = [System.IO.File]::Create($IcoPath)
    $fileStream.Write($iconDir, 0, $iconDir.Length)

    foreach ($entry in $directoryEntries) {
        $fileStream.Write($entry, 0, $entry.Length)
    }

    foreach ($data in $imageData) {
        $fileStream.Write($data, 0, $data.Length)
    }

    $fileStream.Close()

    Write-Host "ICO file created successfully: $IcoPath" -ForegroundColor Green

    # Verify the ICO file was created
    if (Test-Path $IcoPath) {
        $fileSize = [math]::Round((Get-Item $IcoPath).Length / 1KB, 2)
        Write-Host "ICO file size: $fileSize KB" -ForegroundColor Green
    }

    # Create 64x64 PNG for WiX bootstrapper dialog logo
    Write-Host "Creating 64x64 PNG for bootstrapper dialog..." -ForegroundColor Gray
    $logoSize = 64
    $logoBitmap = New-Object System.Drawing.Bitmap($logoSize, $logoSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $logoGraphics = [System.Drawing.Graphics]::FromImage($logoBitmap)

    # Use high-quality resampling (equivalent to Bicubic Sharper)
    $logoGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $logoGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $logoGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $logoGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    # Draw the resized image with high quality
    $logoGraphics.DrawImage($originalImage, 0, 0, $logoSize, $logoSize)
    $logoGraphics.Dispose()

    # Save as PNG-24 with alpha channel (32-bit PNG)
    $logoBitmap.Save($LogoPngPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $logoBitmap.Dispose()

    Write-Host "Logo PNG created successfully: $LogoPngPath" -ForegroundColor Green

    # Verify the PNG file was created
    if (Test-Path $LogoPngPath) {
        $logoFileSize = [math]::Round((Get-Item $LogoPngPath).Length / 1KB, 2)
        Write-Host "Logo PNG file size: $logoFileSize KB" -ForegroundColor Green
    }

} catch {
    Write-Error "Failed to convert PNG to ICO: $($_.Exception.Message)"
    exit 1
} finally {
    # Clean up resources
    if ($bitmaps) {
        foreach ($bitmap in $bitmaps) {
            if ($bitmap) { $bitmap.Dispose() }
        }
    }
    if ($originalImage) { $originalImage.Dispose() }
}

Write-Host "Conversion completed! Both ICO and Logo PNG files ready for installer." -ForegroundColor Blue
