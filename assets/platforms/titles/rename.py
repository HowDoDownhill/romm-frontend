import os

# Path to the directory containing the images
directory = "E:/Projects/romm-frontend/assets/platforms/titles/"

# Mapping of old filenames to new filenames (slugs)
rename_map = {
    "Pico-8.png": "pico-8.png",
    "OpenBOR.png": "openbor.png",
    "Sega CD.png": "segacd.png",
    "Sega 32X.png": "sega32.png",
    "NEC PC-FX.png": "pcfx.png",
    "Atari 2600.png": "atari2600.png",
    "Atari 5200.png": "atari5200.png",
    "Atari 7800.png": "atari7800.png",
    "Casio Loopy.png": "loopy.png",
    "GCE Vectrex.png": "vectrex.png",
    "Nintendo 64.png": "n64.png",
    "Sega CD 32X.png": "segacd32.png",
    "Sega Saturn.png": "saturn.png",
    "SNK Neo Geo.png": "neogeo.png",
    "Apple Pippin.png": "pippin.png",
    "Atari Jaguar.png": "jaguar.png",
    "ColecoVision.png": "coleco.png",
    "Nintendo Wii.png": "wii.png",
    "Philips CD-i.png": "cdiv.png",
    "Sega Genesis.png": "genesis.png",
    "Sega SG-1000.png": "sg1000.png",
    "Casio PV-1000.png": "pv1000.png",
    "NEC PC Engine.png": "pce.png",
    "NEC Turbo Duo.png": "turboduo.png",
    "Nintendo 64DD.png": "n64dd.png",
    "RCA Studio II.png": "studio2.png",
    "Sega Mark III.png": "mark3.png",
    "Amstrad GX4000.png": "gx4000.png",
    "Commodore CDTV.png": "cdtv.png",
    "Microsoft Xbox.png": "xbox.png",
    "Nintendo Wii U.png": "wiiu.png",
    "Sega Dreamcast.png": "dc.png",
    "SNK Neo Geo CD.png": "neocd.png",
    "Atari Jaguar CD.png": "jaguarcd.png",
    "Bally Astrocade.png": "astrocade.png",
    "Nintendo Switch.png": "switch.png",
    "Sega Mega Drive.png": "megadrive.png",
    "Magnavox Odyssey.png": "odyssey.png",
    "NEC PC Engine CD.png": "pcecd.png",
    "Nintendo Famicom.png": "famicom.png",
    "Nintendo WiiWare.png": "wiiware.png",
    "Sony Playstation.png": "psx.png",
    "NEC TurboGrafx-16.png": "tg16.png",
    "NEC TurboGrafx-CD.png": "tg-cd.png",
    "Nintendo GameCube.png": "ngc.png",
    "Sega Mega Mega-CD.png": "megacd.png",
    "Funtech Super Acan.png": "acan.png",
    "Magnavox Odyssey 2.png": "odyssey2.png",
    "Microsoft Xbox 360.png": "xbox360.png",
    "Microsoft Xbox One.png": "xboxone.png",
    "Phillips Videopac+.png": "videopac.png",
    "Sega Master System.png": "sms.png",
    "Sony Playstation 2.png": "ps2.png",
    "Sony Playstation 3.png": "ps3.png",
    "Sony Playstation 4.png": "ps4.png",
    "Sony Playstation 5.png": "ps5.png",
    "VTech CreatiVision.png": "creativision.png",
    "Fairchild Channel F.png": "channelf.png",
    "Sony Playstation VR.png": "psvr.png",
    "Commodore Amiga CD32.png": "amigacd32.png",
    "Emerson Arcadia 2001.png": "arcadia.png",
    "Mattel Intellivision.png": "intellivision.png",
    "Nintendo Satellaview.png": "satellaview.png",
    "Nintendo Virtual Boy.png": "vb.png",
    "Nintendo Wii U eShop.png": "wiiu-eshop.png",
    "Epoch Cassette Vision.png": "cassettevision.png",
    "Microsoft Xbox Series.png": "xbox-series-x.png",
    "Nintendo Sufami Turbo.png": "sufami.png",
    "Nintendo Switch eShop.png": "switch-eshop.png",
    "Fujitsu FM Towns Marty.png": "fmtowns.png",
    "Nintendo Color TV-Game.png": "ctv.png",
    "Nintendo Super Famicom.png": "sfc.png",
    "Nintendo Super Game Boy.png": "sgb.png",
    "Microsoft Xbox Game Pass.png": "xbox-gamepass.png",
    "NEC PC Engine SuperGrafx.png": "supergrafx.png",
    "Nintendo Game Boy Player.png": "gbplayer.png",
    "Sony PlayStation Network.png": "psn.png",
    "Nintendo Super Game Boy 2.png": "sgb2.png",
    "APF Imagination Machine-04.png": "apf-m1000.png",
    "Microsoft Xbox Live Arcade.png": "xbla.png",
    "Sony PlayStation 3 Network.png": "psn-ps3.png",
    "3DO Interactive Multiplayer.png": "3do.png",
    "Epoch Super Cassette Vision.png": "scv.png",
    "Nintendo Famicom Disk System.png": "fds.png",
    "Nintendo Entertainment System.png": "nes.png",
    "Super Nintendo Entertainment System.png": "snes.png"
}

for old_name, new_name in rename_map.items():
    old_path = os.path.join(directory, old_name)
    new_path = os.path.join(directory, new_name)
    if os.path.exists(old_path):
        os.rename(old_path, new_path)
        print(f"Renamed '{old_name}' to '{new_name}'")
        
        # Also rename the .import file
        old_import_path = old_path + ".import"
        new_import_path = new_path + ".import"
        if os.path.exists(old_import_path):
            os.rename(old_import_path, new_import_path)
            print(f"Renamed '{old_name}.import' to '{new_name}.import'")
