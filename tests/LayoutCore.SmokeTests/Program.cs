using System;
using System.Linq;
using PlateUpLayoutEditor.Core;

static ApplianceRecord A(string id, string type, int x, int y, int rotation = 0) =>
    new ApplianceRecord { Id = id, Type = type, X = x, Y = y, Rotation = rotation };
static LayoutDocument D(params ApplianceRecord[] appliances) =>
    new LayoutDocument { Appliances = appliances.ToList() };
static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

var old = D(A("coffee", "CoffeeMachine", 0, 0), A("sink", "Sink", 2, 0));
var target = D(A("coffee", "CoffeeMachine", 1, 0), A("grabber", "Grabber", 3, 0));
var replace = LayoutPlanner.Plan(old, target, ApplyMode.Replace);
Check(replace.IsValid, "Replace should be valid");
Check(replace.Operations.Count(o => o.Kind == OperationKind.Remove) == 1, "Replace removes absent sink");
Check(replace.Operations.Count(o => o.Kind == OperationKind.Move) == 1, "Replace moves coffee");
Check(replace.Operations.Count(o => o.Kind == OperationKind.Create) == 1, "Replace creates grabber");

var merge = LayoutPlanner.Plan(old, target, ApplyMode.Merge);
Check(merge.IsValid, "Merge should be valid");
Check(!merge.Operations.Any(o => o.Kind == OperationKind.Remove), "Merge preserves sink");

var collision = LayoutPlanner.Plan(old, D(A("coffee", "CoffeeMachine", 2, 0)), ApplyMode.Merge);
Check(!collision.IsValid, "Collision should be rejected");

var paste = LayoutPlanner.Plan(old, D(A("coffee", "CoffeeMachine", 0, 0)), ApplyMode.Paste, 5, 5);
Check(paste.IsValid, "Paste should be valid");
Check(paste.Operations.Single().After!.Id != "coffee", "Paste should allocate unique identity");
Check(paste.Operations.Single().After!.X == 5, "Paste should translate coordinates");

var update = LayoutPlanner.Plan(old, D(A("coffee", "CoffeeMachine", 0, 0, 90)), ApplyMode.Merge);
Check(update.IsValid && update.Operations.Single().Kind == OperationKind.Update, "Rotation update");

var duplicate = LayoutPlanner.Plan(old, D(A("x", "Sink", 1, 1), A("x", "Sink", 2, 2)), ApplyMode.Replace);
Check(!duplicate.IsValid, "Duplicate IDs should be rejected");

Console.WriteLine("LayoutCore smoke tests passed.");
