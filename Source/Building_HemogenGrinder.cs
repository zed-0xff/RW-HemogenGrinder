using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using Verse;

using PipeSystem;
using VNPE;

namespace zed_0xff.HemogenGrinder;

// original code copied from VNPE/Building_NutrientGrinder.cs (c) Oscar Potocki

// need to inherit it from Building_NutrientGrinder for proper function of CompRegisterToGrinder
public class Building_HemogenGrinder : Building_NutrientGrinder {
        private const int produceTicksNeeded = 400;

        // only used in Tick() and it's completely overridden
        private Effecter effecter;

        static FieldInfo fi_cachedHoppers = AccessTools.Field(typeof(Building_NutrientGrinder), "cachedHoppers");
        static FieldInfo fi_nextTick      = AccessTools.Field(typeof(Building_NutrientGrinder), "nextTick");

        public List<Thing> CachedHoppers => (List<Thing>)fi_cachedHoppers.GetValue(this);
        public int NextTick {
            get { return (int)fi_nextTick.GetValue(this); }
            set { fi_nextTick.SetValue(this, value); }
        }

        public bool IsAcceptableFeedstock(ThingDef def){
            if( def == ThingDefOf.Meat_Human )
                return true;
            if( def.defName == "RawHemofungus" )
                return true;

            return false;
        }

        public override void Tick()
        {
            var tick = Find.TickManager.TicksGame;
            if (tick >= NextTick)
            {
                NextTick = tick + produceTicksNeeded;
                if (!powerComp.PowerOn || CachedHoppers.NullOrEmpty())
                    return;

                if (TryProduceHemogen() && effecter == null)
                {
                    effecter = EffecterDefOf.EatMeat.Spawn();
                    effecter.Trigger(this, new TargetInfo(Position, Map));
                }
            }
            else if (tick >= NextTick - 150 && effecter != null)
            {
                effecter?.Cleanup();
                effecter = null;
            }

            if (effecter != null)
                effecter.EffectTick(this, new TargetInfo(Position, Map));
        }

        private Thing FindFeedInAnyHopper()
        {
            // from VNPE_Fridge_Fix
            for (int h = 0; h < CachedHoppers.Count; h++)
            {
                // fix #1: process all cells of hopper, mostly for 2x1 fridges, but 2x2 will work too :)
                foreach( var cell in CachedHoppers[h].OccupiedRect() ){
                    var thingList = cell.GetThingList(Map);

                    for (int t = 0; t < thingList.Count; ++t)
                    {
                        Thing thing = thingList[t];
                        if (IsAcceptableFeedstock(thing.def))
                            return thing;
                    }
                }
            }
            return null;
        }

        private bool HasEnoughFeed()
        {
            var map = Map;
            var num = 0f;

            // from VNPE_Fridge_Fix
            for (int h = 0; h < CachedHoppers.Count; h++)
            {
                // fix #1: process all cells of hopper, mostly for 2x1 fridges, but 2x2 will work too :)
                foreach( var cell in CachedHoppers[h].OccupiedRect() ){
                    var things = cell.GetThingList(map);

                    for (int t = 0; t < things.Count; t++)
                    {
                        var thing = things[t];
                        if (IsAcceptableFeedstock(thing.def))
                        {
                            num += thing.stackCount * thing.GetStatValue(StatDefOf.Nutrition);
                            // fix #2: replace break with sufficiency check
                            if (num >= def.building.nutritionCostPerDispense)
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool TryProduceHemogen()
        {
            var net = resourceComp.PipeNet;

            if (net == null || net.AvailableCapacity < 1 || !HasEnoughFeed())
                return false;

            var comps = new List<CompRegisterIngredients>();
            for (int i = 0; i < net.storages.Count; i++)
            {
                var storage = net.storages[i];
                if (storage.parent.GetComp<CompRegisterIngredients>() is CompRegisterIngredients comp)
                    comps.Add(comp);
            }
            var compsCount = comps.Count;

            var num = def.building.nutritionCostPerDispense - 0.00001f;
            while (num > 0)
            {
                var feed = FindFeedInAnyHopper();
                if (feed == null)
                {
                    Log.Error("Did not find enough food in hoppers while trying to grind.");
                    return false;
                }

                for (int i = 0; i < compsCount; i++)
                    comps[i].RegisterIngredient(feed.def);

                var count = Mathf.Min(feed.stackCount, Mathf.CeilToInt(num / feed.GetStatValue(StatDefOf.Nutrition)));
                num -= count * feed.GetStatValue(StatDefOf.Nutrition);
                feed.SplitOff(count);
            }

            net.DistributeAmongStorage(1);
            return true;
        }
}

