# Regenerate Documentation Table of Contents for BankStatementParsing
$docsRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$readmePath = Join-Path $docsRoot 'README.md'

function Get-DocLinks($dir, $prefix = '') {
    Get-ChildItem -Path $dir -File -Filter *.md | Where-Object { $_.Name -ne 'README.md' } | ForEach-Object {
        "- [$(($_.BaseName))]($prefix$($_.Name))"
    }
    Get-ChildItem -Path $dir -Directory | ForEach-Object {
        $subPrefix = "$prefix$($_.Name)/"
        Get-DocLinks -dir $_.FullName -prefix $subPrefix
    }
}

$toc = @()
$toc += '# Documentation Table of Contents'
$toc += ''
$toc += 'Welcome to the BankStatementParsing documentation hub. Use the links below to navigate the available documentation.'
$toc += ''
$toc += Get-DocLinks $docsRoot
$toc += ''
$toc += '---'
$toc += ''
$toc += 'For additional details, refer to the respective markdown files or the main [README](../README.md).'

Set-Content -Path $readmePath -Value $toc -Encoding UTF8
Write-Host "Documentation TOC regenerated in $readmePath" 