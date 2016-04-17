﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FilterExtensions.ConfigNodes
{
    using Utility;
    using KSP.UI.Screens;

    public enum categoryTypeAndBehaviour
    {
        None,
        Engines,
        StockAdd,
        StockReplace,
        ModAdd,
        ModReplace
    }

    public class customCategory : IEquatable<customCategory>
    {
        public string categoryName { get; set; }
        public string iconName { get; set; }
        public Color colour { get; set; }
        public categoryTypeAndBehaviour behaviour { get; set; }
        public bool all { get; set; } // has an all parts subCategory
        public List<subCategoryItem> subCategories { get; set; } // array of subcategories
        public List<Filter> templates { get; set; } // Checks to add to every Filter in a category with the template tag

        private static readonly List<string> categoryNames = new List<string> { "Pods", "Engines", "Fuel Tanks", "Command and Control", "Structural", "Aerodynamics", "Utility", "Science" };

        public customCategory(ConfigNode node)
        {
            bool tmp;
            categoryName = node.GetValue("name");
            iconName = node.GetValue("icon");
            colour = convertToColor(node.GetValue("colour"));

            makeTemplate(node);

            bool.TryParse(node.GetValue("all"), out tmp);
            this.all = tmp;
            
            ConfigNode[] subcategoryList = node.GetNodes("SUBCATEGORIES");
            subCategories = new List<subCategoryItem>();
            if (subcategoryList != null)
            {
                List<subCategoryItem> unorderedSubCats = new List<subCategoryItem>();
                List<string> stringList = new List<string>();
                for (int i = 0; i < subcategoryList.Length; i++)
                    stringList.AddRange(subcategoryList[i].GetValues());
                
                subCategoryItem[] subs = new subCategoryItem[1000];
                for (int i = 0; i < stringList.Count; i++)
                {
                    string[] indexAndValue = stringList[i].Split(',').Select(s => s.Trim()).ToArray();

                    subCategoryItem newSubItem = new subCategoryItem();
                    int index;
                    if (int.TryParse(indexAndValue[0], out index)) // has position index
                    {
                        if (indexAndValue.Length >= 2)
                            newSubItem.subcategoryName = indexAndValue[1];
                        if (string.IsNullOrEmpty(newSubItem.subcategoryName))
                            continue;

                        if (indexAndValue.Length >= 3 && string.Equals(indexAndValue[2], "dont template", StringComparison.CurrentCultureIgnoreCase))
                            newSubItem.applyTemplate = false;
                        subs[index] = newSubItem;
                    }
                    else // no valid position index
                    {
                        newSubItem.subcategoryName = indexAndValue[0];
                        if (string.IsNullOrEmpty(newSubItem.subcategoryName))
                            continue;

                        if (indexAndValue.Length >= 2 && string.Equals(indexAndValue[1], "dont template", StringComparison.CurrentCultureIgnoreCase))
                            newSubItem.applyTemplate = false;
                        unorderedSubCats.Add(newSubItem);
                    }
                }
                subCategories = subs.Distinct().ToList(); // no duplicates and no gaps in a single line. Yay
                subCategories.AddUniqueRange(unorderedSubCats); // tack unordered subcats on to the end
                subCategories.RemoveAll(s => s == null);
            }
            typeSwitch(node.GetValue("type"), node.GetValue("value"));
        }

        public void initialise()
        {
            if (string.IsNullOrEmpty(categoryName))
            {
                Core.Log("Category name is null or empty");
                return;
            }
            if (!hasSubCategories())
            {
                Core.Log(categoryName + " has no subcategories");
                return;
            }
            PartCategorizer.Category category;
            if (behaviour == categoryTypeAndBehaviour.None || behaviour == categoryTypeAndBehaviour.Engines)
            {
                RUI.Icons.Selectable.Icon icon = Core.getIcon(iconName);
                PartCategorizer.AddCustomFilter(categoryName, icon, colour);

                category = PartCategorizer.Instance.filters.Find(c => c.button.categoryName == categoryName);
                category.displayType = EditorPartList.State.PartsList;
                category.exclusionFilter = PartCategorizer.Instance.filterGenericNothing;
            }
            else 
            {
                if (!PartCategorizer.Instance.filters.TryGetValue(c => c.button.categoryName == categoryName, out category))
                {
                    Core.Log("No category of this name was found to manipulate: " + categoryName);
                    return;
                }
                else
                {
                    if (behaviour == categoryTypeAndBehaviour.StockReplace || behaviour == categoryTypeAndBehaviour.ModReplace)
                        category.subcategories.Clear();
                }
            }

            List<string> subcategoryNames = new List<string>();
            for (int i = 0; i < subCategories.Count; i++ )
                subcategoryNames.Add(subCategories[i].subcategoryName);
            
            for (int i = 0; i < subCategories.Count; i++)
            {
                subCategoryItem subcategoryItem = subCategories[i];
                if (subcategoryItem == null)
                    continue;
                
                if (string.IsNullOrEmpty(subcategoryItem.subcategoryName))
                    continue;
                customSubCategory subcategory = null;
                if (!Core.Instance.subCategoriesDict.TryGetValue(subcategoryItem.subcategoryName, out subcategory))
                {
                    Core.Log("subcategory {0} not found in subcategories Dictionary", subcategoryItem.subcategoryName);
                    continue;
                }

                List<string> conflictsList;
                #warning subcategory conflicts are broken and doing stupid things
                //if (Core.Instance.conflictsDict.TryGetValue(subcategoryItem.subcategoryName, out conflictsList))
                //{
                //    // all of the possible conflicts that are also subcategories of this category
                //    List<string> conflicts = conflictsList.Intersect(subcategoryNames).ToList();
                //    // if there are any conflicts that show up in the subcategories list before this one
                //    if (conflicts.Any(c => subcategoryNames.IndexOf(c) < i))
                //    {
                //        Core.Log("Filters duplicated in category " + this.categoryName + " between subCategories:\r\n" + string.Join("\r\n", conflicts.ToArray()));
                //        continue;
                //    }
                //}

                customSubCategory sC = new customSubCategory(subcategory.toConfigNode());
                if (subcategoryItem.applyTemplate)
                    sC.template = templates;

                try
                {
                    if (Core.checkSubCategoryHasParts(sC, categoryName))
                        sC.initialise(category);
                }
                catch (Exception ex)
                {
                    // extended logging for errors
                    Core.Log(subCategories[i] + " failed to initialise");
                    Core.Log("Category:" + categoryName + ", filter:" + sC.hasFilters + ", Count:" + sC.filters.Count + ", Icon:" + Core.getIcon(sC.iconName));
                    Core.Log(ex.StackTrace);
                }
            }
        }

        private void typeSwitch(string type, string value)
        {
            switch (type)
            {
                case "engine":
                    behaviour = categoryTypeAndBehaviour.Engines;
                    foreach (List<string> combo in Core.Instance.propellantCombos)
                    {
                        string dummy = string.Empty, subcatName = string.Join(",", combo.ToArray());
                        Core.Instance.SetNameAndIcon(ref subcatName, ref dummy);
                        subCategories.AddUnique(new subCategoryItem(subcatName));
                    }
                    break;
                case "stock":
                    if (value == "replace")
                        behaviour = categoryTypeAndBehaviour.StockReplace;
                    else
                        behaviour = categoryTypeAndBehaviour.StockAdd;
                    break;
                case "mod":
                    if (value == "replace")
                        behaviour = categoryTypeAndBehaviour.ModReplace;
                    else
                        behaviour = categoryTypeAndBehaviour.ModAdd;
                    break;
                default:
                    behaviour = categoryTypeAndBehaviour.None;
                    break;
            }
        }

        private void makeTemplate(ConfigNode node)
        {
            ConfigNode[] filtNodes = node.GetNodes("FILTER");
            if (filtNodes == null)
                return;
            templates = new List<Filter>();
            foreach (ConfigNode n in filtNodes)
                templates.Add(new Filter(n));
        }

        public static Color convertToColor(string hex_ARGB)
        {
            if (string.IsNullOrEmpty(hex_ARGB))
                return Color.clear;

            hex_ARGB = hex_ARGB.Replace("#", "").Replace("0x", ""); // remove any hexadecimal identifiers
            if (System.Text.RegularExpressions.Regex.IsMatch(hex_ARGB, "[0-9a-fA-F]{6,8}")) // check it is valid hex
            {
                if (hex_ARGB.Length == 8)
                {
                    Color c = new Color();
                    c.a = (float)byte.Parse(hex_ARGB.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                    c.r = (float)byte.Parse(hex_ARGB.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                    c.g = (float)byte.Parse(hex_ARGB.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                    c.b = (float)byte.Parse(hex_ARGB.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                    return c;
                }
                else // if (hex_ARGB.Length == 6)
                {
                    Color c = new Color();
                    c.a = 1;
                    c.r = (float)byte.Parse(hex_ARGB.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                    c.g = (float)byte.Parse(hex_ARGB.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                    c.b = (float)byte.Parse(hex_ARGB.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                    return c;
                }
            }
            return Color.clear;
        }

        public bool hasSubCategories()
        {
            return (subCategories != null && subCategories.Any());
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((customCategory)obj);
        }

        public bool Equals(customCategory C)
        {
            if (ReferenceEquals(null, C))
                return false;
            if (ReferenceEquals(this, C))
                return true;

            return categoryName.Equals(C.categoryName);
        }

        public override int GetHashCode()
        {
            return categoryName.GetHashCode();
        }
    }

    public class subCategoryItem : IEquatable<subCategoryItem>
    {
        public string subcategoryName { get; set; }
        public bool applyTemplate { get; set; }

        public subCategoryItem()
        {
            applyTemplate = true;
        }
        public subCategoryItem(string name, bool useTemplate = true)
        {
            subcategoryName = name;
            applyTemplate = useTemplate;
        }

        public bool Equals(subCategoryItem sub)
        {
            if (ReferenceEquals(null, sub))
                return false;
            if (ReferenceEquals(this, sub))
                return true;

            return subcategoryName.Equals(sub.subcategoryName);
        }

        public override int GetHashCode()
        {
            return subcategoryName.GetHashCode();
        }

        public override string ToString()
        {
            return subcategoryName;
        }
    }
}
