﻿using KSP.Localization;
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FilterExtensions
{
    public class SubCategoryInstance
    {
        public string Name { get; }
        public string Icon { get; }
        bool UnpurchasedVisible { get; }
        HashSet<AvailablePart> Parts { get; } = new HashSet<AvailablePart>();

        /// <summary>
        /// subcategory is only valid if there are visible parts
        /// </summary>
        public bool Valid { get => Parts.Any(); }

        /// <summary>
        /// generate instance from the configuration prototype
        /// </summary>
        /// <param name="protoNode"></param>
        /// <param name="allParts"></param>
        public SubCategoryInstance(ConfigNodes.SubcategoryNode protoNode, List<AvailablePart> allParts)
        {
            Name = Localizer.Format(protoNode.SubCategoryTitle);
            Icon = protoNode.IconName;
            UnpurchasedVisible = protoNode.UnPurchasedOverride;
            foreach (AvailablePart p in allParts)
            {
                if (protoNode.CheckPartFilters(p))
                {
                    Parts.Add(p);
                }
            }
        }

        /// <summary>
        /// called in the editor when creating the subcategory
        /// </summary>
        /// <param name="cat">The category to add this subcategory to</param>
        public void Initialise(PartCategorizer.Category cat, bool debug )
        {
#if DEBUG
            if (debug)
                UnityEngine.Debug.Log("Initialize, cat: " + cat);
#endif
            if (cat == null)
            {
                return;
            }
            RUI.Icons.Selectable.Icon icon = IconLib.GetIcon(this);
            PartCategorizer.AddCustomSubcategoryFilter(cat, Name, Localizer.Format(Name), icon, p => TestPart(p));
        }

        /// <summary>
        /// callback to determine whether part is visible in this subcategory
        /// </summary>
        /// <param name="ap"></param>
        /// <returns></returns>
        bool TestPart(AvailablePart ap)
        {
#if DEBUG
            UnityEngine.Debug.Log("TestPart, name: " + ap.name + ", title: " + ap.title);
#endif
            if (!UnpurchasedVisible && HighLogic.CurrentGame.Parameters.CustomParams<Settings>().hideUnpurchased 
                && !(ResearchAndDevelopment.PartModelPurchased(ap) || ResearchAndDevelopment.IsExperimentalPart(ap)))
            {
                return false;
            }
#if DEBUG
            UnityEngine.Debug.Log("Parts.Contains: " + Parts.Contains(ap));
#endif
            return Parts.Contains(ap);
        }
    }
}
