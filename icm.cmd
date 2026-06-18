@echo off
rem icm - the console CLI (open/chat/mcp/validate/gen). PATH-friendly. Runs in-memory to satisfy
rem Smart App Control. Example: icm validate examples\d2-mini skills
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-cli.ps1" %*
