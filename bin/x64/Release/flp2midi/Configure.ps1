Set-Location $PSScriptRoot
Add-Type -AssemblyName System.Windows.Forms
$browsefiles = New-Object System.Windows.Forms.OpenFileDialog
$browsefiles.Filter = "FL Studio Project Files (*.flp)|*.flp"
$browsefiles.Title = "Please Select Your FL Studio Project File..."
if ($browsefiles.ShowDialog() -eq 'OK'){
$form = New-Object System.Windows.Forms.Form
$form.Text = "flp2midi v1.4.3"
$form.Size = New-Object System.Drawing.Size(300,380)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedToolWindow"
$form.TopMost = $true
$title = New-Object System.Windows.Forms.Label
$title.Font = "dosis,18"
$title.Text = "flp2midi v1.4.3"
$title.TextAlign = "MiddleCenter"
$title.Size = "300,35"
$title.Location = New-Object System.Drawing.Point(0,20)
$label1 = New-Object System.Windows.Forms.Label
$label1.Font = "dosis,12"
$label1.Text = "options: "
$label1.TextAlign = "MiddleLeft"
$label1.Size = "70,20"
$label1.Location = New-Object System.Drawing.Point(15,70)
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
$label2 = New-Object System.Windows.Forms.Label
$label2.Font = "dosis,10"
$label2.Text = "conversion mode: "
$label2.TextAlign = "MiddleLeft"
$label2.Size = "110,20"
$label2.Location = New-Object System.Drawing.Point(15,250)
$dropdown = New-Object System.Windows.Forms.ComboBox
$dropdown.Font = "dosis,9"
$dropdown.Size = New-Object System.Drawing.Size(270,40)
$dropdown.Location = New-Object System.Drawing.Size(10,270)
$dropdown.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
$dropdown.Items.Add("Mode A  -  One FL track in playlist -> One MIDI track")
$dropdown.Items.Add("Mode B  -  One MIDI Out in channel rack -> One MIDI track")
$dropdown.Items.Add("Mode C  -  One MIDI Out in one FL track -> One MIDI track")
$dropdown.SelectedItem = "Mode A  -  One FL track in playlist -> One MIDI track"
$button = New-Object System.Windows.Forms.Button
$button.Text = "Execute"
$button.Font = "dosis,12"
$button.TextAlign = "MiddleCenter"
$button.Location = New-Object System.Drawing.Point(200,310)
$button.Add_Click({
$arguments = @()
$arguments += "/k .\flp2midi.exe"
$arguments += "`"$($browsefiles.FileName)`""
if ($dropdown.SelectedItem -eq "Mode A  -  One FL track in playlist -> One MIDI track") { $arguments += " A" }
if ($dropdown.SelectedItem -eq "Mode B  -  One MIDI Out in channel rack -> One MIDI track") { $arguments += " B" }
if ($dropdown.SelectedItem -eq "Mode C  -  One MIDI Out in one FL track -> One MIDI track") { $arguments += " C" }
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
$form.Controls.Add($label1)
$form.Controls.Add($label2)
$form.Controls.Add($checkbox1)
$form.Controls.Add($checkbox2)
$form.Controls.Add($checkbox3)
$form.Controls.Add($checkbox4)
$form.Controls.Add($checkbox5)
$form.Controls.Add($dropdown)
$form.Controls.Add($button)
[Windows.Forms.Application]::Run($form)
}