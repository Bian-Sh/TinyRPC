@echo off
tree /f /a > temp.txt
type temp.txt | findstr /v /i "\.meta$" > 1.txt
del temp.txt
