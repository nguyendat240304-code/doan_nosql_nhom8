@echo off
title Caddy HTTPS cho doan-nhom8-nosql.duckdns.org (V1)
cd /d "%~dp0"

echo ======================================================================
echo    KHOI CHAY CADDY HTTPS CHO WEB V1 (PORT 5000)
echo ======================================================================
echo.

:: 1. Mo Firewall Windows Port 80 va 443
netsh advfirewall firewall add rule name="Allow HTTP Port 80" dir=in action=allow protocol=TCP localport=80 >nul 2>&1
netsh advfirewall firewall add rule name="Allow HTTPS Port 443" dir=in action=allow protocol=TCP localport=443 >nul 2>&1
echo [+] Da kiem tra va mo Port 80, 443 tren Windows Firewall!

:: 2. Ghi de Caddyfile chuan xac cho V1 (Port 5000)
echo doan-nhom8-nosql.duckdns.org { > Caddyfile
echo     reverse_proxy 127.0.0.1:5000 >> Caddyfile
echo } >> Caddyfile
echo [+] Da tao Caddyfile cho doan-nhom8-nosql.duckdns.org (Port 5000)!

:: 3. Khoi chay Caddy
echo.
echo ======================================================================
echo [*] Dang khoi dong Caddy va xin chung chi SSL Let's Encrypt...
echo [!] LUU Y: CUA SO NAY PHAI DUOC GIU MO DE DUY TRI HTTPS!
echo ======================================================================
echo.

caddy.exe run --config Caddyfile
pause
