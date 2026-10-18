$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../scripts/config.ps1')
$fixture = @'
[config]
value_right_align = true
font_name = 微软雅黑
[task_bar]
plugin_display_item =
auto_save_taskbar_color_settings_to_preset = true
horizontal_arrange = false
value_right_align = true
item_space = 8
font_size = 9
[other]
item_space = 99
'@
$result = Set-CodexQuotaLayout $fixture
if ($result -notmatch '(?m)^plugin_display_item = CodexQuota\.5h,CodexQuota\.7d\r?$') { throw 'Blank value consumed next line' }
if ($result -notmatch '(?m)^auto_save_taskbar_color_settings_to_preset = true\r?$') { throw 'Unrelated setting changed' }
if ($result -notmatch '(?m)^value_right_align = false\r?$' -or $result -notmatch '(?m)^item_space = 12\r?$') { throw 'Layout not applied' }
if ($result -notmatch '\[config\]\r?\nvalue_right_align = true' -or $result -notmatch '\[other\]\r?\nitem_space = 99') { throw 'Other sections modified' }
if ($result -notmatch 'font_name = 微软雅黑' -or $result -notmatch 'font_size = 9') { throw 'Font changed' }
if ((Set-CodexQuotaLayout $result) -cne $result) { throw 'Repeated installation is not idempotent' }
$broken = $fixture.Replace('plugin_display_item =', 'plugin_display_item = ThirdParty.Item,auto_save_taskbar_color_settings_to_preset = true,CodexQuota.5h,')
$fixed = Set-CodexQuotaLayout $broken
if ($fixed -notmatch '(?m)^plugin_display_item = ThirdParty.Item,CodexQuota.5h,CodexQuota.7d\r?$') { throw 'Existing plugin lost or spurious item not removed' }
Write-Output 'PASS: blank value, unrelated settings, left alignment, group gap, section isolation, fonts, idempotence, existing plugins, and migration.'
