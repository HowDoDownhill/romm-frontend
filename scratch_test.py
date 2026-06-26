import re

s = '-batch -fullscreen -portable -bigpicture -bios "{bios_path}" -- "{rom_path}"'
res = re.sub(r'\s*-+[-a-zA-Z0-9_]+\s+"?\{bios_path\}"?', '', s)
print("Original:", s)
print("Result:", res)
