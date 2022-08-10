import json
import random

enemyList = ["RW_Lemonhead","M_MercWiener_Scout","M_Swat_Riflewiener","M_Swat_Scout","RW_Beefkicker","RW_Pig","RW_Rot"]

enemyType = enemyList[6]

# Data to be written
dictionary = {
    "Name": "All in One (" + enemyType + ")",
    "Description": "The entire H3VR arsenal",
    "OrderType": 1,
    "EnemyType": enemyType, #"M_MercWiener_Shield",
    "Guns": [],
    "GunNames": [],
    "MagNames": [],
    "CategoryIDs": []
}

BlackList = ["MF_Syringegun","Degle","PotatoGun","Stinger","COOLCLOSEDBOLT","COOLREVOLVER","GrappleGun",
            "GravitonBeamer","PlungerLauncher","BrownBess","BrownBessRamrod","HeavyFlintlock18thCentury",
            "HeavyFlintlock18thCenturyRamrod", "SustenanceCrossbow", "MF_Flamethrower", "MF_Medical180", 
            "MF_LongShot", "Pocket1906", "PocketHammer1903","OTS38", "Jackhammer", "Flaregun",#"LadiesPepperbox",
            "JunkyardFlameThrower","M72A7","M320GrenadeLauncher", "M224Mortar","SP5K","SP5KA2","SP5KA3",
            "SP5KFolding","Whizzbanger","P6Twelve"]
mag_BL =    ["MagazineMp515rnd","MagazineAK74_10rnd","MagazineStanag10rnd","MagazineStanag5rnd","MagazineVZ58_10Rnd",
            "MagazineMp515rndStraight"]
cartridge_BL = ["12GaugeShellCannonball","12GaugeShellFreedomfetti","12GaugeShellRedFlare","23x75mmR_Flash","20GaugeShellFreedomfetti","Cartridge_40mmCaseless_TPM", "Cartridge_16Gauge_Freedomfetti"]
clip_BL =   []

# Opening JSON file
with open('ObjectData.json', 'r') as openfile:
    # Reading from json file
    json_object = json.load(openfile)

mag_list = []
for obj in json_object:
    if obj['Category'] == "Magazine" and not obj["IsModContent"] and not obj["ObjectID"] in mag_BL:
        mag_list.append(obj)

clip_list = []
for obj in json_object:
    if obj['Category'] == "Clip" and not obj["IsModContent"] and not obj["ObjectID"] in clip_BL:
        clip_list.append(obj)

cartridge_list = []
for obj in json_object:
    if obj['Category'] == "Cartridge" and not obj["IsModContent"] and not obj["ObjectID"] in cartridge_BL:
        cartridge_list.append(obj)

for obj in json_object:
    if obj['Category'] == "Firearm" and not obj["IsModContent"] and not obj["ObjectID"] in BlackList:
        if obj["MagazineType"] != 0:
            random.shuffle(mag_list)
            dictionary["CategoryIDs"].append(0)
            for mag_obj in mag_list:
                if mag_obj["MagazineType"] == obj["MagazineType"]:
                    dictionary["MagNames"].append(mag_obj['ObjectID'])
                    break
        elif obj["ClipType"] != 0:
            random.shuffle(clip_list)
            dictionary["CategoryIDs"].append(1)
            for clip_obj in clip_list:
                if clip_obj["ClipType"] == obj["ClipType"]:
                    dictionary["MagNames"].append(clip_obj['ObjectID'])
                    break
        elif obj["RoundType"] != 0:
            random.shuffle(cartridge_list)
            dictionary["CategoryIDs"].append(2)
            for cartridge_obj in cartridge_list:
                if cartridge_obj["RoundType"] == obj["RoundType"]:
                    dictionary["MagNames"].append(cartridge_obj['ObjectID'])
                    break
        else:
            print(obj["ObjectID"] + " has 000 mag type")
            dictionary["CategoryIDs"].append(2)
            dictionary["MagNames"].append("22LRCartridgeTracer")
        dictionary["GunNames"].append(obj["ObjectID"])

# dictionary["GunNames"] = dictionary["GunNames"][344:]
# dictionary["MagNames"] = dictionary["MagNames"][344:]
# dictionary["CategoryIDs"] = dictionary["CategoryIDs"][344:]
print(len(dictionary["GunNames"]))
print(len(dictionary["MagNames"]))
print(len(dictionary["CategoryIDs"]))
print("Mag weapon count", dictionary["CategoryIDs"].count(0))
print("Clip weapon count", dictionary["CategoryIDs"].count(1))
print("Cartridge weapon count", dictionary["CategoryIDs"].count(2))

 
# Serializing json
json_object = json.dumps(dictionary, indent=4)
 
# Writing to sample.json
json_name = "GunGameWeaponPool_All in One (" + enemyType + ").json"
with open(json_name, "w") as outfile:
    outfile.write(json_object)