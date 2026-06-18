@echo off
rem icm-gui - open the GUI on a folder (default: current dir). PATH-friendly: add this folder to
rem PATH and run "icm-gui ." from any directory. Runs in-memory to satisfy Smart App Control.
powershell -STA -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-gui.ps1" -Folder "%~1"
