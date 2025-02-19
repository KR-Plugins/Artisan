﻿using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.MacroSystem;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Condition = Artisan.CraftingLogic.CraftData.Condition;

namespace Artisan.CraftingLogic
{
    public static unsafe class CurrentCraft
    {
        public static event EventHandler<int>? StepChanged;
        public static int CurrentDurability { get; set; } = 0;
        public static int MaxDurability { get; set; } = 0;
        public static int CurrentProgress { get; set; } = 0;
        public static int MaxProgress { get; set; } = 0;
        public static int CurrentQuality { get; set; } = 0;
        public static int MaxQuality { get; set; } = 0;
        public static int HighQualityPercentage { get; set; } = 0;
        public static string? RecommendationName { get; set; }
        public static Condition CurrentCondition { get; set; }
        public static int CurrentStep
        {
            get { return currentStep; }
            set
            {
                if (currentStep != value)
                {
                    currentStep = value;
                    StepChanged?.Invoke(currentStep, value);
                    P.TM.Abort();
                }

            }
        }
        public static string? HQLiteral { get; set; }
        public static bool CanHQ { get; set; }
        public static string? CollectabilityLow { get; set; }
        public static string? CollectabilityMid { get; set; }
        public static string? CollectabilityHigh { get; set; }
        public static string? ItemName { get; set; }
        public static Recipe? CurrentRecipe { get; set; }
        public static uint CurrentRecommendation { get; set; }
        public static bool CraftingWindowOpen { get; set; } = false;
        public static bool JustUsedFinalAppraisal { get; set; } = false;
        public static bool JustUsedObserve { get; set; } = false;
        public static bool JustUsedGreatStrides { get; set; } = false;
        public static bool ManipulationUsed { get; set; } = false;
        public static bool WasteNotUsed { get; set; } = false;
        public static bool InnovationUsed { get; set; } = false;
        public static bool VenerationUsed { get; set; } = false;
        public static bool BasicTouchUsed { get; set; } = false;
        public static bool StandardTouchUsed { get; set; } = false;
        public static bool AdvancedTouchUsed { get; set; } = false;
        public static bool ExpertCraftOpenerFinish { get; set; } = false;
        public static int QuickSynthCurrent
        {
            get => quickSynthCurrent;
            set
            {
                if (value != 0 && quickSynthCurrent != value)
                {
                    CraftingListFunctions.CurrentIndex++;
                    if (P.Config.QuickSynthMode && Endurance.Enable && P.Config.CraftX > 0)
                        P.Config.CraftX--;
                }
                quickSynthCurrent = value;
            }
        }
        public static int QuickSynthMax { get; set; } = 0;
        public static int MacroStep { get; set; } = 0;
        public static bool DoingTrial { get; set; } = false;
        public static CraftingState State
        {
            get { return state; }
            set
            {
                if (value != state)
                {
                    if (state == CraftingState.Crafting)
                    {
                        bool wasSuccess = SolverLogic.CheckForSuccess();
                        if (!P.Config.QuickSynthMode && !wasSuccess && P.Config.EnduranceStopFail && Endurance.Enable)
                        {
                            Endurance.Enable = false;
                            Svc.Toasts.ShowError("You failed a craft. Disabling Endurance.");
                            DuoLog.Error("You failed a craft. Disabling Endurance.");
                        }

                        if (!P.Config.QuickSynthMode && P.Config.EnduranceStopNQ && !LastItemWasHQ && LastCraftedItem != null && !LastCraftedItem.IsCollectable && LastCraftedItem.CanBeHq && Endurance.Enable)
                        {
                            Endurance.Enable = false;
                            Svc.Toasts.ShowError("You crafted a non-HQ item. Disabling Endurance.");
                            DuoLog.Error("You crafted a non-HQ item. Disabling Endurance.");
                        }
                    }


                    if (value == CraftingState.NotCrafting || value == CraftingState.PreparingToCraft)
                    {
                        CraftingWindow.MacroTime = new();
                    }

                    if (value == CraftingState.Crafting)
                    {
                        if (CraftingWindow.MacroTime.Ticks <= 0 && P.Config.IRM.ContainsKey((uint)Endurance.RecipeID) && P.Config.UserMacros.TryGetFirst(x => x.ID == P.Config.IRM[(uint)Endurance.RecipeID], out var macro))
                        {
                            Double timeInSeconds = MacroUI.GetMacroLength(macro); // Counting crafting duration + 2 seconds between crafts.
                            CraftingWindow.MacroTime = TimeSpan.FromSeconds(timeInSeconds);
                        }

                        if (P.ws.Windows.Any(x => x.GetType() == typeof(MacroEditor)))
                        {
                            foreach (var window in P.ws.Windows.Where(x => x.GetType() == typeof(MacroEditor)))
                            {
                                window.IsOpen = false;
                            }
                        }
                    }
                }
                state = value;
            }
        }

        private static int currentStep = 0;
        private static int quickSynthCurrent = 0;
        private static CraftingState state = CraftingState.NotCrafting;
        public static bool LastItemWasHQ = false;
        public static Item? LastCraftedItem;
        public static uint PreviousAction = 0;

        public unsafe static bool GetCraft()
        {
            try
            {
                var quickSynthPTR = Svc.GameGui.GetAddonByName("SynthesisSimple", 1);
                if (quickSynthPTR != IntPtr.Zero)
                {
                    var quickSynthWindow = (AtkUnitBase*)quickSynthPTR;
                    if (quickSynthWindow != null)
                    {
                        try
                        {
                            var currentTextNode = (AtkTextNode*)quickSynthWindow->UldManager.NodeList[20];
                            var maxTextNode = (AtkTextNode*)quickSynthWindow->UldManager.NodeList[18];

                            QuickSynthCurrent = Convert.ToInt32(currentTextNode->NodeText.ToString());
                            QuickSynthMax = Convert.ToInt32(maxTextNode->NodeText.ToString());
                        }
                        catch
                        {

                        }
                        return true;
                    }
                }
                else
                {
                    QuickSynthCurrent = 0;
                    QuickSynthMax = 0;
                }

                IntPtr synthWindow = Svc.GameGui.GetAddonByName("Synthesis", 1);
                if (synthWindow == IntPtr.Zero)
                {
                    CurrentStep = 0;
                    CharacterInfo.IsCrafting = false;
                    return false;
                }

                var craft = Marshal.PtrToStructure<AddonSynthesis>(synthWindow);
                if (craft.Equals(default(AddonSynthesis))) return false;
                if (craft.ItemName == null) { CraftingWindowOpen = false; return false; }

                CraftingWindowOpen = true;

                var cd = *craft.CurrentDurability;
                var md = *craft.StartingDurability;
                var mp = *craft.MaxProgress;
                var cp = *craft.CurrentProgress;
                var cq = *craft.CurrentQuality;
                var mq = *craft.MaxQuality;
                var hqp = *craft.HQPercentage;
                var cond = *craft.Condition;
                var cs = *craft.StepNumber;
                var hql = *craft.HQLiteral;
                var collectLow = *craft.CollectabilityLow;
                var collectMid = *craft.CollectabilityMid;
                var collectHigh = *craft.CollectabilityHigh;
                var item = *craft.ItemName;

                DoingTrial = craft.AtkUnitBase.UldManager.NodeList[99]->IsVisible;
                CharacterInfo.IsCrafting = true;
                CurrentDurability = Convert.ToInt32(cd.NodeText.ToString());
                MaxDurability = Convert.ToInt32(md.NodeText.ToString());
                CurrentProgress = Convert.ToInt32(cp.NodeText.ToString());
                MaxProgress = Convert.ToInt32(mp.NodeText.ToString());
                CurrentQuality = Convert.ToInt32(cq.NodeText.ToString());
                MaxQuality = Convert.ToInt32(mq.NodeText.ToString());
                ItemName = item.NodeText.ExtractText();
                //ItemName = ItemName.Remove(ItemName.Length - 10, 10);
                if (ItemName[^1] == '')
                {
                    ItemName = ItemName.Remove(ItemName.Length - 1, 1).Trim();
                }

                if (CurrentRecipe is null || CurrentRecipe.ItemResult.Value.Name.ExtractText() != ItemName)
                {
                    var sheetItem = LuminaSheets.RecipeSheet?.Values.Where(x => x.ItemResult.Value.Name!.ExtractText().Equals(ItemName) && x.CraftType.Value.RowId == CharacterInfo.JobID - 8).FirstOrDefault();
                    if (sheetItem != null)
                    {
                        CurrentRecipe = sheetItem;
                    }
                }
                if (CurrentRecipe != null)
                {
                    if (CurrentRecipe.CanHq)
                    {
                        CanHQ = true;
                        HighQualityPercentage = Convert.ToInt32(hqp.NodeText.ToString());
                    }
                    else
                    {
                        CanHQ = false;
                        HighQualityPercentage = 0;
                    }
                }


                CurrentCondition = Condition.Unknown;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[229].Text.RawString) CurrentCondition = Condition.Poor;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[227].Text.RawString) CurrentCondition = Condition.Good;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[226].Text.RawString) CurrentCondition = Condition.Normal;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[228].Text.RawString) CurrentCondition = Condition.Excellent;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[239].Text.RawString) CurrentCondition = Condition.Centered;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[240].Text.RawString) CurrentCondition = Condition.Sturdy;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[241].Text.RawString) CurrentCondition = Condition.Pliant;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[13455].Text.RawString) CurrentCondition = Condition.Malleable;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[13454].Text.RawString) CurrentCondition = Condition.Primed;
                if (LuminaSheets.AddonSheet.ContainsKey(14214) && cond.NodeText.ToString() == LuminaSheets.AddonSheet[14214].Text.RawString) CurrentCondition = Condition.GoodOmen;

                CurrentStep = Convert.ToInt32(cs.NodeText.ToString());
                HQLiteral = hql.NodeText.ToString();
                CollectabilityLow = collectLow.NodeText.ToString().GetNumbers().Length == 0 ? "0" : collectLow.NodeText.ToString().GetNumbers();
                CollectabilityMid = collectMid.NodeText.ToString().GetNumbers().Length == 0 ? "0" : collectMid.NodeText.ToString().GetNumbers();
                CollectabilityHigh = collectHigh.NodeText.ToString().GetNumbers().Length == 0 ? "0" : collectHigh.NodeText.ToString().GetNumbers();

                return true;


            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, ex.StackTrace!);
                return false;
            }
        }
    }
}
