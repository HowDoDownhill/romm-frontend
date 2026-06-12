import os
from PIL import Image

for filename in os.listdir('.'):
    if filename.endswith('.ico'):
        img = Image.open(filename)
        # Some .ico files have multiple sizes, we just save the largest one
        img.save(filename[:-4] + '.png', format='PNG')