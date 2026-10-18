function Set-CodexQuotaLayout {
    param([Parameter(Mandatory)][string]$Content)
    # Limit edits to the taskbar section. Horizontal whitespace must never consume a newline.
    $section = [regex]::Match($Content, '(?ms)^\[task_bar\][ \t]*\r?\n(?<body>.*?)(?=^\[|\z)')
    if (!$section.Success) { throw 'No [task_bar] section found.' }
    $body = $section.Groups['body'].Value
    $itemsLine = [regex]::Match($body, '(?m)^plugin_display_item[ \t]*=[ \t]*([^\r\n]*)')
    if (!$itemsLine.Success) { throw 'No plugin_display_item setting; configure this version manually.' }
    $items = @($itemsLine.Groups[1].Value.Split(',') | ForEach-Object { $_.Trim() } | Where-Object {
        $_ -and $_ -notmatch '^auto_save_taskbar_color_settings_to_preset[ \t]*=[ \t]*true$'
    })
    $items = @($items + @('CodexQuota.5h','CodexQuota.7d') | Select-Object -Unique)
    $settings = [ordered]@{
        plugin_display_item = ($items -join ',')
        horizontal_arrange = 'false'
        value_right_align = 'false'
        item_space = '12'
    }
    foreach ($key in $settings.Keys) {
        $pattern = '(?m)^' + [regex]::Escape($key) + '[ \t]*=[^\r\n]*'
        if (![regex]::IsMatch($body, $pattern)) { throw "Missing taskbar setting: $key" }
        $line = $key + ' = ' + $settings[$key]
        $body = [regex]::Replace($body, $pattern, [Text.RegularExpressions.MatchEvaluator]{ param($m) $line })
    }
    $start = $section.Groups['body'].Index
    return $Content.Substring(0, $start) + $body + $Content.Substring($start + $section.Groups['body'].Length)
}
