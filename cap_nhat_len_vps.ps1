# ==============================================================================
# SCRIPT CAP NHAT DO AN NOSQL V2 LEN VPS ORACLE CLOUD
# ==============================================================================
# Mo PowerShell tren VPS va copy/paste dong lenh duoi day de cap nhat tu dong trong 5 giay:

Stop-Process -Name 'doan_cuoiky_nosql' -Force -ErrorAction SilentlyContinue
$zipPath = "$HOME\Desktop\update_v2.zip"
$destPath = "$HOME\Desktop\doan_nosql_v2_hoanchinh"

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   DANG DOWLOAD BAN CAP NHAT SELF-CONTAINED MOI NHAT..." -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$url = "https://github.com/nguyendat240304-code/doan_nosql_nhom8/releases/download/v2.2/doan_nosql_v2_hoanchinh.zip"
(New-Object System.Net.WebClient).DownloadFile($url, $zipPath)

Write-Host "-> Da tai xong! Dang giai nen ghi de vao Desktop..." -ForegroundColor Cyan
Expand-Archive -Path $zipPath -DestinationPath $destPath -Force
Remove-Item $zipPath -Force

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "   CAP NHAT VPS THANH CONG 100%! SAN SANG KHOI DONG!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
