$r = Invoke-RestMethod -Uri 'https://romm.gargalindis.com/api/roms?id=15099' -Headers @{'Authorization'='Bearer rmm_5f61d487a99df7b8bdc92ac33e6e9e3f22865771d0f7e1b689980d17040dc6b4'}
$r | ConvertTo-Json -Depth 10 | Out-File E:\Projects\romm-frontend\scratch_romm.json
