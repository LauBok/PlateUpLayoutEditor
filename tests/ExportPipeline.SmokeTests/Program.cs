using System;
using System.Collections.Generic;
using System.Linq;
using PlateUpLayoutEditor.Core;

sealed class FakeRestaurant : IReadOnlyRestaurantSource
{
    public IEnumerable<ApplianceSnapshot> ReadAppliances() =>
        new[]
        {
            new ApplianceSnapshot { StableId = "coffee-1", ApplianceType = "CoffeeMachine", GridX = 1, GridY = 2, RotationDegrees = 90 },
            new ApplianceSnapshot { StableId = "grabber-1", ApplianceType = "SmartGrabber", GridX = 2, GridY = 2, RotationDegrees = 90,
                State = new Dictionary<string, object> { ["filter"] = "Coffee" } }
        };
}

var doc = ExportPipeline.Capture(new FakeRestaurant(), "Test restaurant");
if (doc.Version != 1 || doc.Appliances.Count != 2) throw new Exception("Unexpected export");
if (doc.Appliances.Single(a => a.Id == "grabber-1").Rotation != 90) throw new Exception("Lost orientation");
var json = ExportPipeline.ToJson(doc);
if (!json.Contains("CoffeeMachine") || !json.Contains("filter")) throw new Exception("Missing exported fields");
Console.WriteLine("Export pipeline smoke test passed");
