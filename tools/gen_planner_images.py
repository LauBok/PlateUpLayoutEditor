"""Generate editor/planner-images.js: which PlateUp! Planner picture shows which appliance.

Pictures are NOT copied; the editor loads them from https://plateupplanner.github.io/images/display/
(top-down renders from the PlateUp! Planner project, github.com/plateupplanner/plateupplanner.github.io).

Usage: python3 tools/gen_planner_images.py <planner checkout>/src/utils/helpers.tsx editor/catalog.js
Each entry lists the game appliance codes it matches. The editor also matches by display name, which
covers appliances missing from the built-in catalog once the in-game catalog.json is imported.
"""
import json, re, sys

helpers, catalog_js = sys.argv[1], sys.argv[2]
planner = re.findall(r"\['(\w\w)', \['([\w\-]+\.png)', '([^']+)'\]\]", open(helpers, encoding="utf-8").read())
s = open(catalog_js, encoding="utf-8").read()
codes = {e["code"] for e in json.loads(s[s.index("["):s.rindex("]") + 1])}

# Planner name -> game appliance codes, where the names differ.
MANUAL = {
    "Safety Hob": ["HobSafe"], "Gas Override": ["GasSafetyOverride"],
    "Counter": ["Countertop"], "Starter Plates": ["PlateStackStarting"], "Plates": ["PlateStack"],
    "Autoplater": ["AutoPlater"], "Serving Boards": ["ServingBoardStack"], "Woks": ["WokStack"],
    "Kitchen Floor Protector": ["FloorProtector"], "Rolling Pin": ["RollingPinProvider"],
    "Sharp Knife": ["SharpKnifeProvider"], "Dining Table": ["TableLarge"], "Bar Table": ["TableBar"],
    "Metal Table": ["TableCheapMetal"], "Table - Simple Cloth": ["TableBasicCloth"],
    "Table - Fancy Cloth": ["TableFancyCloth"], "Breadsticks": ["BreadstickBox"], "Napkins": ["NapkinBox"],
    '"Specials" Menu': ["SpecialsMenuBox"], "Supplies": ["SupplyCabinet"], "Starter Sink": ["SinkStarting"],
    "Sink": ["SinkNormal"], "Soaking Sink": ["SinkSoak"], "Power Sink": ["SinkPower"], "Wash Basin": ["SinkLarge"],
    "Dishwasher": ["DishWasher"], "Mop": ["MopBucket"], "Lasting Mop": ["MopBucketLasting"],
    "Fast Mop": ["MopBucketFast"], "Floor Buffer": ["FloorBufferStation"], "Dish Rack": ["Rack"],
    "Scrubbing Brush": ["ScrubbingBrushProvider"], "Conveyor": ["Belt"],
    # Rotating grabber: picture chosen by its grab direction in the editor.
    "Corner Grabber (Straight)": ["GrabberRotatable"],
    "Conveyor Mixer": ["MixerPusher"], "Heated Mixer": ["MixerHeated"], "Rapid Mixer": ["MixerRapid"],
    "Research Desk": ["BlueprintUpgradeDesk"], "Blueprint Desk": ["BlueprintOrderingDesk"],
    "Copying Desk": ["BlueprintCopyDesk"], "Discount Desk": ["BlueprintDiscountDesk"],
    "Trainers": ["ShoeRackTrainers"], "Wellies": ["ShoeRackWellies"], "Work Boots": ["ShoeRackWorkBoots"],
    "Display Stand": ["FoodDisplayStand"], "Specials Terminal": ["OrderingTerminalSpecialOffers"],
    "Hosting Stand": ["HostStand"], "Cake Stand": ["SourceCakeStand"], "Tortillas": ["ProviderTortilla"],
    "Burger Patties": ["SourceBurgerPatty"], "Cherries": ["SourceCherry"], "Cranberries": ["SourceCranberry"],
    "Bone-in Steaks": ["SourceBonedMeat"], "Olives": ["SourceOlive"], "Peppers": ["SourceBellPeppers"],
    "Teabags": ["SourceTeaBags"], "Teacups": ["SourceTeaCups"], "Teapots": ["SourceTeaPots"],
    "Banana": ["SourceBananas"], "Pumpkins": ["SourcePumpkin"], "Wine": ["SourceWine"], "Milk": ["SourceMilk"],
    "Hotdogs": ["SourceHotdog"], "Hotdog Buns": ["SourceHotdogBun"], "Meat": ["SourceMeat"],
    "Onions": ["SourceOnion"], "Strawberry": ["SourceStrawberry"], "Chocolate": ["SourceChocolate"],
    "Apples": ["SourceApple"], "Ice Cream": ["SourceIceCream"], "Plant": ["Plant"],
    "Hob": ["Hob"], "Oven": ["Oven"], "Bin": ["Bin"], "Grabber": ["Grabber"], "Mixer": ["Mixer"],
    "Prep Station": ["PrepStation"], "Chair": ["Chair"], "Robot Mop": ["RobotMop"], "Robot Buffer": ["RobotBuffer"],
    "Ordering Terminal": ["OrderingTerminal"], "Coffee": ["CoffeeMachine"],
    "Beans": ["SourceBeans"], "Burger Buns": ["SourceBurgerBuns"], "Christmas Crackers": ["SourceChristmasCracker"],
    "Nuts": ["SourceNuts"], "Potatoes": ["SourcePotato"], "Tomatoes": ["SourceTomato"],
}
SKIP = {"Empty tile", "Door", "Corner Grabber (Left)", "Corner Grabber (Right)"}

def words(t):
    t = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", t).lower()
    return set(re.sub(r"[^a-z0-9 ]", " ", t).split())

entries = []
for code, img, name in planner:
    if name in SKIP:
        continue
    match = MANUAL.get(name)
    if match is None:
        ws = words(name.replace("Starter", "Starting"))
        exact = [c for c in codes if words(c) == ws]
        singular = {w[:-1] if w.endswith("s") and len(w) > 3 else w for w in ws}
        source = [c for c in codes if words(c) == {"source"} | singular]
        match = exact or source
    match = [c for c in match if c in codes]
    entries.append({"img": img, "name": name, "planner": code, "codes": match})

entries.append({"img": "corner-grabber-left.png", "name": "Corner Grabber (Left)", "planner": "3V", "codes": [], "pushDirection": "Left"})
entries.append({"img": "corner-grabber-right.png", "name": "Corner Grabber (Right)", "planner": "U7", "codes": [], "pushDirection": "Right"})

with open("editor/planner-images.js", "w", encoding="utf-8") as f:
    f.write("// Generated by tools/gen_planner_images.py. Pictures are loaded at runtime from the PlateUp! Planner\n")
    f.write("// site (https://github.com/plateupplanner/plateupplanner.github.io); none are stored in this repository.\n")
    f.write("globalThis.PLANNER_IMAGES = {\n  base: 'https://plateupplanner.github.io/images/display/',\n  entries: [\n")
    for e in entries:
        f.write("    " + json.dumps(e, ensure_ascii=False) + ",\n")
    f.write("  ],\n};\n")
mapped = sum(1 for e in entries if e["codes"])
print(f"{len(entries)} pictures, {mapped} mapped to catalog codes")
print("unmapped:", [e["name"] for e in entries if not e["codes"] and "pushDirection" not in e])
