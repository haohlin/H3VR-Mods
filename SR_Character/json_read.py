import json

# Data to be written
dictionary = {
    "Name": "All_in_One_Only_New",
    "Description": "The entire H3VR arsenal (Only new guns)",
    "OrderType": 1,
    "EnemyType": "RW_Rot", 
    "Guns": [],
    "GunNames": [],
    "MagNames": [],
    "CategoryIDs": []
}

# Opening JSON file
with open('GunGameWeaponPool_All_in_One_RW_Rot.json', 'r') as openfile1:
    # Reading from json file
    new_json_object = json.load(openfile1)

with open('GunGameWeaponPool_OLD.json', 'r') as openfile2:
    # Reading from json file
    old_json_object = json.load(openfile2)

new_count = 0
for i, new_gun in enumerate(new_json_object["GunNames"]):
    if new_gun not in old_json_object["GunNames"]:
        new_count += 1
        print("new_gun: ", new_gun, " index: ", i)
        dictionary["GunNames"].append(new_gun)
        dictionary["MagNames"].append(new_json_object["MagNames"][i])
        dictionary["CategoryIDs"].append(new_json_object["CategoryIDs"][i])

# Serializing json
json_object = json.dumps(dictionary, indent=4)
 
# Writing to sample.json
json_name = "GunGameWeaponPool_" + dictionary["Name"] + ".json"
with open(json_name, "w") as outfile:
    outfile.write(json_object)

print(new_count)