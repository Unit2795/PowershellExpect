$cmdKeyPath = "HKCU:\Console"
$screenBufferSize = (Get-ItemProperty -Path $cmdKeyPath).ScreenBufferSize

# The width is the low-order word
$defaultWidth = $screenBufferSize -band 0xFFFF
# The height is the high-order word, requiring a shift
$defaultHeight = ($screenBufferSize -shr 16) -band 0xFFFF

Write-Output "Default Width: $defaultWidth, Default Height: $defaultHeight"
