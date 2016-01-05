﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using KSP;
using Strategies;
using Strategies.Effects;

namespace Strategia
{
    /// <summary>
    /// Strategy for giving experience to new hires.
    /// </summary>
    public class NewKerbalExperience : StrategyEffect
    {
        public const string SPECIAL_XP = "SpecialTraining";

        /// <summary>
        /// Static initializer to hack the kerbal experience/flight log system to add our entries.
        /// </summary>
        static NewKerbalExperience()
        {
            Debug.Log("Strategia: Setting up Kerbal Experience");

            FieldInfo[] fields = typeof(KerbalRoster).GetFields(BindingFlags.NonPublic | BindingFlags.Static);

            foreach (FieldInfo field in fields)
            {
                object value = field.GetValue(null);
                IEnumerable<string> strValues = value as IEnumerable<string>;
                if (strValues != null)
                {
                    // We're looking for the Kerbin lists that contain Training, but not PlantFlag
                    if (strValues.Contains("Training") && !strValues.Contains("PlantFlag"))
                    {
                        List<string> newValues = strValues.ToList();
                        // Allow up to 5 levels (max level)
                        for (int i = 1; i <= 5; i++)
                        {
                            newValues.Add(SPECIAL_XP + i);
                        }
                        field.SetValue(null, newValues.ToArray());
                    }
                    // Also there's the printed version
                    else if (strValues.Contains("Train at") && !strValues.Contains("Plant flag on"))
                    {
                        List<string> newValues = strValues.ToList();
                        // Allow up to 5 levels (max level)
                        for (int i = 1; i <= 5; i++)
                        {
                            newValues.Add("Special training on");
                        }
                        field.SetValue(null, newValues.ToArray());
                    }

                    continue;
                }

                IEnumerable<float> floatValues = value as IEnumerable<float>;
                if (floatValues != null)
                {
                    // Get the list of experience points for the above string entries
                    if (floatValues.First() == 1.0f && !floatValues.Contains(2.3f))
                    {
                        List<float> newValues = floatValues.ToList();
                        // Allow the 5 levels
                        newValues.Add(2.0f);
                        newValues.Add(8.0f);
                        newValues.Add(16.0f);
                        newValues.Add(32.0f);
                        newValues.Add(64.0f);
                        field.SetValue(null, newValues.ToArray());
                    }

                    continue;
                }
            }
        }

        int level;
        ProtoCrewMember.Gender? gender;
        string trait;

        public NewKerbalExperience(Strategy parent)
            : base(parent)
        {
        }

        protected override string GetDescription()
        {
            string genderStr = gender != null ? gender.Value.ToString().ToLower() + " " : "";
            string astronautStr = string.IsNullOrEmpty(trait) ? "astronauts" : (trait.ToLower() + "s");

            return "Hired " + genderStr + astronautStr + " start at level " + Parent.Level() + ".";
        }

        protected override void OnLoadFromConfig(ConfigNode node)
        {
            level = ConfigNodeUtil.ParseValue<int>(node, "level", 0);
            gender = ConfigNodeUtil.ParseValue<ProtoCrewMember.Gender?>(node, "gender", null);
            trait = ConfigNodeUtil.ParseValue<string>(node, "trait", (string)null);
        }

        protected override void OnRegister()
        {
            GameEvents.onKerbalTypeChange.Add(new EventData<ProtoCrewMember, ProtoCrewMember.KerbalType, ProtoCrewMember.KerbalType>.OnEvent(OnKerbalTypeChange));
        }

        protected override void OnUnregister()
        {
            GameEvents.onKerbalTypeChange.Remove(new EventData<ProtoCrewMember, ProtoCrewMember.KerbalType, ProtoCrewMember.KerbalType>.OnEvent(OnKerbalTypeChange));
        }

        private void OnKerbalTypeChange(ProtoCrewMember pcm, ProtoCrewMember.KerbalType oldType, ProtoCrewMember.KerbalType newType)
        {
            if (oldType == ProtoCrewMember.KerbalType.Applicant && newType == ProtoCrewMember.KerbalType.Crew)
            {
                // Check for correct trait
                if (!string.IsNullOrEmpty(trait) && pcm.experienceTrait.Config.Name != trait)
                {
                    return;
                }

                // Check for correct gender
                if (gender != null && pcm.gender != gender.Value)
                {
                    return;
                }

                CelestialBody homeworld = FlightGlobals.Bodies.Where(cb => cb.isHomeWorld).FirstOrDefault();

                Debug.Log("Strategia: Awarding experience to " + pcm.name);

                // Find existing entries
                int currentValue = 2;
                foreach (FlightLog.Entry entry in pcm.careerLog.Entries.Concat(pcm.flightLog.Entries).Where(e => e.type.Contains(SPECIAL_XP)))
                {
                    // Get the entry with the largest value
                    int entryValue = Convert.ToInt32(entry.type.Substring(SPECIAL_XP.Length, entry.type.Length - SPECIAL_XP.Length));
                    currentValue = Math.Max(currentValue, entryValue);
                }

                // Get the experience level
                int value = Parent.Level();
                string type = SPECIAL_XP + value.ToString();

                // Do the awarding
                pcm.flightLog.AddEntry(type, homeworld.name);
                pcm.ArchiveFlightLog();

                // Force the astronaut complex GUI to refresh so we actually see the experience
                CMAstronautComplex ac = UnityEngine.Object.FindObjectOfType<CMAstronautComplex>();
                if (ac != null)
                {
                    MethodInfo updateListMethod = typeof(CMAstronautComplex).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).
                        Where(mi => mi.Name == "CreateAvailableList").First();
                    updateListMethod.Invoke(ac, new object[] { });

                    MethodInfo addToListMethod = typeof(CMAstronautComplex).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).
                        Where(mi => mi.Name == "AddItem_Available").First();
                    addToListMethod.Invoke(ac, new object[] { pcm });
                }
            }
        }
    }
}
