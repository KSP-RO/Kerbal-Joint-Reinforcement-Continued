using KSP.IO;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalJointReinforcement
{
    public class KJRSettings : GameParameters.CustomParameterNode
    {
        public override string Title => "General Options";

        public override string DisplaySection => "KJR";

        public override string Section => "KJR";

        public override int SectionOrder => 1;

        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;

        public override bool HasPresets => false;

        [GameParameters.CustomParameterUI("Toggle stiffening of all vessel joints", autoPersistance = false)]
        public bool reinforceAttachNodes = true;

        [GameParameters.CustomParameterUI("Toggle additional stiffening", autoPersistance = false,
            toolTip = "Toggles additional stiffening by connecting parts in a stack one part further, but at a weaker strength")]
        public bool multiPartAttachNodeReinforcement = true;

        [GameParameters.CustomParameterUI("Toggle stiffening of interstage connections", autoPersistance = false)]
        public bool reinforceDecouplersFurther = true;

        [GameParameters.CustomParameterUI("Toggle stiffening of launch clamp connections", autoPersistance = false)]
        public bool reinforceLaunchClampsFurther = true;

        [GameParameters.CustomParameterUI("Toggle clamp connections that are completely rigid", autoPersistance = false,
            toolTip = "With this option enabled, even the heaviest of rockets shouldn't move a millimeter when loaded onto the launch pad")]
        public bool clampJointHasInfiniteStrength = false;

        [GameParameters.CustomParameterUI("Calculate by area", autoPersistance = false,
            toolTip = "Switches to calculating connection area based on volume, not area; not technically correct, but allows a better approximation of very large rockets")]
        public bool useVolumeNotArea = true;

        [GameParameters.CustomParameterUI("Toggle debug info", autoPersistance = false,
            toolTip = "Toggles debug output to log; please activate and provide log if making a bug report")]
        public bool debug = false;

        // The following fields are marked as internal so that the values wouldn't get persisted to the save file
        internal float angularMaxForceFactor = 0;

        internal float breakForceMultiplier = 1;
        internal float breakTorqueMultiplier = 1;

        internal float breakStrengthPerArea = 40;
        internal float breakTorquePerMOI = 40000;
        internal float surfaceAttachAreaMult = 10;
        internal float surfaceAttachMOIMult = 10;

        internal float decouplerAndClampJointStrength = float.PositiveInfinity;

        internal float stiffeningExtensionMassRatioThreshold = 5;

        internal float massForAdjustment = 0.001f;

        internal List<string> exemptPartTypes = new List<string>();
        internal List<string> exemptModuleTypes = new List<string>();
        internal List<string> decouplerStiffeningExtensionType = new List<string>();

        private static PluginConfiguration config;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (debug) Debug.Log("[KJR] KJRSettings OnLoad called");

            if (config == null)
            {
                config = PluginConfiguration.CreateForType<KJRManager>();
                config.load();
            }

            reinforceAttachNodes = config.GetValue(nameof(reinforceAttachNodes), true);
            multiPartAttachNodeReinforcement = config.GetValue(nameof(multiPartAttachNodeReinforcement), true);
            reinforceDecouplersFurther = config.GetValue(nameof(reinforceDecouplersFurther), true);
            reinforceLaunchClampsFurther = config.GetValue(nameof(reinforceLaunchClampsFurther), true);
            clampJointHasInfiniteStrength = config.GetValue(nameof(clampJointHasInfiniteStrength), false);
            useVolumeNotArea = config.GetValue(nameof(useVolumeNotArea), true);
            debug = config.GetValue(nameof(debug), false);

            angularMaxForceFactor = config.GetValue(nameof(angularMaxForceFactor), angularMaxForceFactor);
            if (angularMaxForceFactor < 0)
                angularMaxForceFactor = float.MaxValue;

            breakForceMultiplier = config.GetValue(nameof(breakForceMultiplier), breakForceMultiplier);
            breakTorqueMultiplier = config.GetValue(nameof(breakTorqueMultiplier), breakTorqueMultiplier);

            breakStrengthPerArea = config.GetValue(nameof(breakStrengthPerArea), breakStrengthPerArea);
            breakTorquePerMOI = config.GetValue(nameof(breakTorquePerMOI), breakTorquePerMOI);

            decouplerAndClampJointStrength = config.GetValue(nameof(decouplerAndClampJointStrength), decouplerAndClampJointStrength);
            if (decouplerAndClampJointStrength < 0)
                decouplerAndClampJointStrength = float.PositiveInfinity;

            stiffeningExtensionMassRatioThreshold = config.GetValue(nameof(stiffeningExtensionMassRatioThreshold), stiffeningExtensionMassRatioThreshold);

            massForAdjustment = config.GetValue(nameof(massForAdjustment), massForAdjustment);

            exemptPartTypes.Clear();
            exemptModuleTypes.Clear();
            decouplerStiffeningExtensionType.Clear();

            int i = 0;
            do
            {
                string tmpPart, tmpModule, tmpDecoupler;
                tmpPart = config.GetValue("exemptPartType" + i, "");
                tmpModule = config.GetValue("exemptModuleType" + i, "");
                tmpDecoupler = config.GetValue("decouplerStiffeningExtensionType" + i, "");

                if (tmpPart == "" && tmpModule == "" && tmpDecoupler == "")
                    break;

                if (tmpPart != "")
                    exemptPartTypes.Add(tmpPart);
                if (tmpModule != "")
                    exemptModuleTypes.Add(tmpModule);
                if (tmpDecoupler != "")
                    decouplerStiffeningExtensionType.Add(tmpDecoupler);

                i++;
            } while (true);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            if (config != null)
            {
                try
                {
                    bool isDirty = false;
                    isDirty |= UpdateConfigValue(config, "reinforceAttachNodes", reinforceAttachNodes);
                    isDirty |= UpdateConfigValue(config, "multiPartAttachNodeReinforcement", multiPartAttachNodeReinforcement);
                    isDirty |= UpdateConfigValue(config, "reinforceDecouplersFurther", reinforceDecouplersFurther);
                    isDirty |= UpdateConfigValue(config, "reinforceLaunchClampsFurther", reinforceLaunchClampsFurther);
                    isDirty |= UpdateConfigValue(config, "clampJointHasInfiniteStrength", clampJointHasInfiniteStrength);
                    isDirty |= UpdateConfigValue(config, "useVolumeNotArea", useVolumeNotArea);
                    isDirty |= UpdateConfigValue(config, "debug", debug);

                    if (isDirty)
                    {
                        if (debug) Debug.Log("[KJR] Config is dirty, updating...");
                        config.save();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private bool UpdateConfigValue<T>(PluginConfiguration config, string name, T value)
        {
            if (!value.Equals(config.GetValue(name, value)))
            {
                config.SetValue(name, value);
                return true;
            }
            return false;
        }
    }
}
