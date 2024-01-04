Set-Location $PSScriptRoot
Add-Type -AssemblyName System.Windows.Forms
$browsefiles = New-Object System.Windows.Forms.OpenFileDialog
$browsefiles.Filter = "FL Studio Project Files (*.flp)|*.flp"
$browsefiles.Title = "Please Select Your FL Studio Project File..."
if ($browsefiles.ShowDialog() -eq 'OK'){
$form = New-Object System.Windows.Forms.Form
$form.Text = "flp2midi v1.4.1"
$form.Size = New-Object System.Drawing.Size(300,320)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedToolWindow"
$form.TopMost = $true
$title = New-Object System.Windows.Forms.Label
$title.Font = "dosis,18"
$title.Text = "flp2midi v1.4.1"
$title.TextAlign = "MiddleCenter"
$title.Size = "300,35"
$title.Location = New-Object System.Drawing.Point(0,20)
$text = New-Object System.Windows.Forms.Label
$text.Font = "dosis,12"
$text.Text = "options: "
$text.TextAlign = "MiddleLeft"
$text.Size = "70,20"
$text.Location = New-Object System.Drawing.Point(15,70)
$checkbox1 = New-Object System.Windows.Forms.CheckBox
$checkbox1.Text = "Enforce mapping color to channel"
$checkbox1.Font = "dosis,10"
$checkbox1.TextAlign = "MiddleLeft"
$checkbox1.Size = "250,20"
$checkbox1.Location = New-Object System.Drawing.Point(15,100)
$checkbox2 = New-Object System.Windows.Forms.CheckBox
$checkbox2.Text = "Echo effect (beta)"
$checkbox2.Font = "dosis,10"
$checkbox2.TextAlign = "MiddleLeft"
$checkbox2.Size = "250,20"
$checkbox2.Location = New-Object System.Drawing.Point(15,130)
$checkbox3 = New-Object System.Windows.Forms.CheckBox
$checkbox3.Text = "Export muted patterns"
$checkbox3.Font = "dosis,10"
$checkbox3.TextAlign = "MiddleLeft"
$checkbox3.Size = "250,20"
$checkbox3.Location = New-Object System.Drawing.Point(15,160)
$checkbox4 = New-Object System.Windows.Forms.CheckBox
$checkbox4.Text = "132 keys"
$checkbox4.Font = "dosis,10"
$checkbox4.TextAlign = "MiddleLeft"
$checkbox4.Size = "250,20"
$checkbox4.Location = New-Object System.Drawing.Point(15,190)
$checkbox5 = New-Object System.Windows.Forms.CheckBox
$checkbox5.Text = "Full velocity"
$checkbox5.Font = "dosis,10"
$checkbox5.TextAlign = "MiddleLeft"
$checkbox5.Size = "250,20"
$checkbox5.Location = New-Object System.Drawing.Point(15,220)
$button = New-Object System.Windows.Forms.Button
$button.Text = "Execute"
$button.Font = "dosis,12"
$button.TextAlign = "MiddleCenter"
$button.Location = New-Object System.Drawing.Point(200,250)
$button.Add_Click({
$arguments = @()
$arguments += "/k .\flp2midi.exe "
$arguments += $browsefiles.FileName
if ($checkbox1.Checked) { $arguments += " -c" }
if ($checkbox2.Checked) { $arguments += " -e" }
if ($checkbox3.Checked) { $arguments += " -m" }
if ($checkbox4.Checked) { $arguments += " -x" }
if ($checkbox5.Checked) { $arguments += " -f" }
$form.Close()
Write-Host "Arguments: $($arguments)"
Start-Process -FilePath C:\Windows\System32\cmd.exe -ArgumentList $arguments
})
$form.Controls.Add($title)
$form.Controls.Add($text)
$form.Controls.Add($checkbox1)
$form.Controls.Add($checkbox2)
$form.Controls.Add($checkbox3)
$form.Controls.Add($checkbox4)
$form.Controls.Add($checkbox5)
$form.Controls.Add($button)
[Windows.Forms.Application]::Run($form)
}