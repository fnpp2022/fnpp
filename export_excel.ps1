$excelFile = "c:\Users\Duran\.gemini\antigravity\scratch\fnpp\CSV profili\Registro FNPP.xlsx"
$outputFolder = "c:\Users\Duran\.gemini\antigravity\scratch\fnpp\tmp_csv_excel"

if (!(Test-Path -Path $outputFolder)) {
    New-Item -ItemType Directory -Path $outputFolder | Out-Null
}

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

try {
    $workbook = $excel.Workbooks.Open($excelFile)
    
    foreach ($worksheet in $workbook.Worksheets) {
        $sheetName = $worksheet.Name
        # Replace invalid filename characters
        $safeName = $sheetName -replace '[<>:"/\\|?*]', '_'
        $csvPath = Join-Path -Path $outputFolder -ChildPath "$safeName.csv"
        
        # 6 is xlCSV
        $worksheet.SaveAs($csvPath, 6)
        Write-Host "Exported: $sheetName"
    }
    
    $workbook.Close($false)
}
catch {
    Write-Error $_.Exception.Message
}
finally {
    $excel.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
}
