import json
import random
import argparse

parser = argparse.ArgumentParser(description="Select Sosig type")
parser.add_argument("sosigType", type=int, default=0)
args = parser.parse_args()

enemyList = ["RW_Lemonhead","M_MercWiener_Scout","M_Swat_Riflewiener","RW_Beefkicker","RW_Pig","RW_Rot"]

enemyType = enemyList[args.sosigType]

# Data to be written
dictionary = {
    "Name": "All in One (" + enemyType + ")",
    "Description": "The entire H3VR arsenal",
    "OrderType": 1,
    "EnemyType": enemyType,
    "Guns": [],
    "GunNames": [],
    "MagNames": [],
    "CategoryIDs": []
}

BlackList = ["MF_Syringegun","Degle","PotatoGun","Stinger","COOLCLOSEDBOLT","COOLREVOLVER","GrappleGun",
            "GravitonBeamer","PlungerLauncher","BrownBess","BrownBessRamrod","HeavyFlintlock18thCentury",
            "HeavyFlintlock18thCenturyRamrod", "SustenanceCrossbow", "MF_Flamethrower", "MF_Medical180", 
            "MF_LongShot", "Pocket1906", "PocketHammer1903","OTS38", "Jackhammer", "Flaregun",
            "JunkyardFlameThrower","M72A7","M320GrenadeLauncher", "M224Mortar","SP5K","SP5KA2","SP5KA3",
            "Whizzbanger","P6Twelve","MF_Signaler","MP5SFA2","MP5SD1","MP5SD2","MP5SD3","MP5SD5",
            "MP5SD4","MP5K","MP5KA2","MP5KA3","MP510A4","MP540A4","MP5A2","MP5A3","MP5A4","MP5KN",]
mag_BL =    ["MagazineMp515rnd","MagazineAK74_10rnd","MagazineStanag10rnd","MagazineAKMTactical10rnd",
            "MagazineStanag5rnd","MagazineVZ58_10Rnd","MagazineMp515rndStraight", "MagazineMini145rnd",
            "MagazineMini1410rnd","MagazineVSSVintorez10rnd","MagazineEvo315rnd","MagazineModel38_10rnd"]
cartridge_BL =  ["12GaugeShellCannonball","12GaugeShellFreedomfetti","12GaugeShellRedFlare","23x75mmR_Flash",
                "20GaugeShellFreedomfetti","Cartridge_40mmCaseless_TPM", "Cartridge_16Gauge_Freedomfetti",
                "Cartridge50mmFlareClassic", "Cartridge50mmFlareConflagration","Cartridge50mmFlareDangerClose",
                "Cartridge50mmFlareSunburn","27x90mmCartridgeSmokescreen","27x90mmCartridgeTriFlash",
                "Cartridge366UltraMagnumSalute","Cartridge366UltraMagnumRetort", "18x50mmPackawhallop_Gobsmacka",
                "Cartridge_40mmCaseless_SMK","Cartridge_40mmCaseless_SF1","Cartridge366UltraMagnumDebuff",
                "Cartridge_40x46Grenade_M651","23x75mmR_CSGas","Cartridge_40x46Grenade_M781","Cartridge_40x46Grenade_X1776"]
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
        MagNameFound = 0
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