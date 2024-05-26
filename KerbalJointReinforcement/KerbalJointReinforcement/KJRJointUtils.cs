/*
Kerbal Joint Reinforcement, v3.3.3
Copyright 2015, Michael Ferrara, aka Ferram4

    This file is part of Kerbal Joint Reinforcement.

    Kerbal Joint Reinforcement is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Kerbal Joint Reinforcement.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KerbalJointReinforcement
{
    public static class KJRJointUtils
    {
        public static KJRSettings settings;

        public static List<ConfigurableJoint> GetJointListFromAttachJoint(PartJoint partJoint)
        {
            /*FieldInfo[] fields = partJoint.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            List<ConfigurableJoint> jointList = new List<ConfigurableJoint>();
            Type jointListType = jointList.GetType();

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType == jointListType)
                {
                    return (List<ConfigurableJoint>)field.GetValue(partJoint);

                }
            }*/

            return partJoint.joints;
        }

        public static bool IsJointAdjustmentValid(Part p)
        {
            for (int i = 0; i < p.Modules.Count; i++)
            {
                var pm = p.Modules[i];
                if (pm is IJointLockState jls && jls.IsJointUnlocked() || settings.exemptModuleTypes.Contains(pm.ClassName))
                    return false;
            }

            return true;
        }

        public static bool GetsDecouplerStiffeningExtension(Part p)
        {
            foreach (string s in settings.decouplerStiffeningExtensionType)
            {
                if (p.Modules.Contains(s))
                {
                    return true;
                }
            }

            return false;
        }

        public static List<Part> GetDecouplerPartStiffeningList(Part p, bool childrenNotParent, bool onlyAddLastPart)
        {
            List<Part> tmpPartList = new List<Part>();
            bool extend = false;
            // non-physical parts are skipped over by attachJoints, so do the same
            if (p.physicalSignificance == Part.PhysicalSignificance.NONE)
                extend = true;
            if (!extend)
                extend = GetsDecouplerStiffeningExtension(p);

            List<Part> newAdditions = new List<Part>();
            if (extend)
            {
                if (childrenNotParent)
                {
                    foreach (Part q in p.children)
                    {
                        if (q != null && q.parent == p)
                        {
                            newAdditions.AddRange(GetDecouplerPartStiffeningList(q, childrenNotParent, onlyAddLastPart));
                        }
                    }
                }
                else if (p.parent)
                {
                    newAdditions.AddRange(GetDecouplerPartStiffeningList(p.parent, childrenNotParent, onlyAddLastPart));
                }
            }
            else
            {
                double thisPartMaxMass = p.physicsMass;
                if (thisPartMaxMass > 0)
                {
                    if (childrenNotParent)
                    {
                        foreach (Part q in p.children)
                        {
                            if (q != null && q.parent == p)
                            {
                                double massRatio = q.physicsMass / thisPartMaxMass;
                                //if (massRatio < 1)
                                //    massRatio = 1 / massRatio;

                                if (massRatio > settings.stiffeningExtensionMassRatioThreshold)
                                {
                                    newAdditions.Add(q);
                                    if (settings.debug)
                                        Debug.Log($"[KJR] Part {q.partInfo.title} added to list due to mass ratio difference");
                                }
                            }
                        }
                    }
                    else if (p.parent)
                    {
                        double massRatio = p.parent.physicsMass / thisPartMaxMass;
                        //if (massRatio < 1)
                        //    massRatio = 1 / massRatio;

                        if (massRatio > settings.stiffeningExtensionMassRatioThreshold)
                        {
                            newAdditions.Add(p.parent);
                            if (settings.debug)
                                Debug.Log($"[KJR] Part {p.parent.partInfo.title} added to list due to mass ratio difference");
                        }
                    }
                }
            }
            if (newAdditions.Count > 0)
                tmpPartList.AddRange(newAdditions);
            else if (onlyAddLastPart)
                extend = false;

            if (!onlyAddLastPart || !extend)
                tmpPartList.Add(p);

            return tmpPartList;
        }

        public static void ConnectLaunchClampToGround(Part clamp)
        {
            float breakForce = Mathf.Infinity;
            float breakTorque = Mathf.Infinity;

            FixedJoint newJoint = clamp.gameObject.AddComponent<FixedJoint>();

            newJoint.connectedBody = null;
            newJoint.anchor = Vector3.zero;
            newJoint.axis = Vector3.up;
            //newJoint.secondaryAxis = Vector3.forward;
            newJoint.breakForce = breakForce;
            newJoint.breakTorque = breakTorque;

            //newJoint.xMotion = newJoint.yMotion = newJoint.zMotion = ConfigurableJointMotion.Locked;
            //newJoint.angularXMotion = newJoint.angularYMotion = newJoint.angularZMotion = ConfigurableJointMotion.Locked;
        }

        public static void AddLaunchClampReinforcementModule(Part p)
        {
            var pm = (KJRLaunchClampReinforcementModule)p.AddModule(nameof(KJRLaunchClampReinforcementModule));
            pm.clampJointHasInfiniteStrength = settings.clampJointHasInfiniteStrength;
            pm.OnPartUnpack();
            if (settings.debug)
                Debug.Log("[KJR] Added KJRLaunchClampReinforcementModule to part " + p.partInfo.title);
        }

        public static void LoadConstants()
        {
            settings = HighLogic.CurrentGame.Parameters.CustomParams<KJRSettings>();

            if (settings.debug)
            {
                StringBuilder debugString = new StringBuilder();
                debugString.AppendLine($"\n\rMax Force Factor: {settings.angularMaxForceFactor}");

                debugString.AppendLine($"\n\rJoint Strength Multipliers: \n\rForce Multiplier: {settings.breakForceMultiplier}\n\rTorque Multiplier: {settings.breakTorqueMultiplier}");
                debugString.AppendLine("Joint Force Strength Per Unit Area: " + settings.breakStrengthPerArea);
                debugString.AppendLine("Joint Torque Strength Per Unit MOI: " + settings.breakTorquePerMOI);

                debugString.AppendLine("Strength For Additional Decoupler And Clamp Joints: " + settings.decouplerAndClampJointStrength);

                debugString.AppendLine("Reinforce Attach Nodes: " + settings.reinforceAttachNodes);
                debugString.AppendLine("Reinforce Decouplers Further: " + settings.reinforceDecouplersFurther);
                debugString.AppendLine("Reinforce Launch Clamps Further: " + settings.reinforceLaunchClampsFurther);
                debugString.AppendLine("Clamp Joint Has Infinite Strength: " + settings.clampJointHasInfiniteStrength);
                debugString.AppendLine("Use Volume For Calculations, Not Area: " + settings.useVolumeNotArea);

                debugString.AppendLine("\n\rMass For Joint Adjustment: " + settings.massForAdjustment);

                debugString.AppendLine("\n\rExempt Module Types");
                foreach (string s in settings.exemptModuleTypes)
                    debugString.AppendLine(s);

                debugString.AppendLine("\n\rDecoupler Stiffening Extension Types");
                foreach (string s in settings.decouplerStiffeningExtensionType)
                    debugString.AppendLine(s);

                debugString.AppendLine("\n\rDecoupler Stiffening Extension Mass Ratio Threshold: " + settings.stiffeningExtensionMassRatioThreshold);

                Debug.Log(debugString.ToString());
            }
        }

        public static Vector3 GuessUpVector(Part part)
        {
            // For intakes, use the intake vector
            if (part.Modules.Contains<ModuleResourceIntake>())
            {
                ModuleResourceIntake i = part.Modules.GetModule<ModuleResourceIntake>();
                Transform intakeTrans = part.FindModelTransform(i.intakeTransformName);
                return part.transform.InverseTransformDirection(intakeTrans.forward);
            }
            // If surface attachable, and node normal is up, check stack nodes or use forward
            else if (part.srfAttachNode != null &&
                     part.attachRules.srfAttach &&
                     Mathf.Abs(part.srfAttachNode.orientation.normalized.y) > 0.9f)
            {
                // When the node normal is exactly Vector3.up, the editor orients forward along the craft axis
                Vector3 dir = Vector3.forward;
                bool first = true;

                foreach (AttachNode node in part.attachNodes)
                {
                    // Doesn't seem to ever happen, but anyway
                    if (node.nodeType == AttachNode.NodeType.Surface)
                        continue;

                    // If all node orientations agree, use that axis
                    if (first)
                    {
                        first = false;
                        dir = node.orientation.normalized;
                    }
                    // Conflicting node directions - bail out
                    else if (Mathf.Abs(Vector3.Dot(dir, node.orientation.normalized)) < 0.9f)
                        return Vector3.up;
                }

                if (settings.debug)
                    MonoBehaviour.print($"{part.partInfo.title}: Choosing axis {dir} for KJR surface attach{(first ? "" : " from node")}.");

                return dir;
            }
            else
            {
                return Vector3.up;
            }
        }

        public static Vector3 CalculateExtents(Part p, Vector3 up)
        {
            up = up.normalized;

            // Align y axis of the result to the 'up' vector in local coordinate space
            if (Mathf.Abs(up.y) < 0.9f)
                return CalculateExtents(p, Quaternion.FromToRotation(Vector3.up, up));

            return CalculateExtents(p, Quaternion.identity);
        }

        public static Vector3 CalculateExtents(Part p, Vector3 up, Vector3 forward)
        {
            // Adjust forward to be orthogonal to up; LookRotation might do the opposite
            Vector3.OrthoNormalize(ref up, ref forward);

            // Align y to up and z to forward in local coordinate space
            return CalculateExtents(p, Quaternion.LookRotation(forward, up));
        }

        public static Vector3 CalculateExtents(Part p, Quaternion alignment)
        {
            Vector3 maxBounds = new Vector3(-100, -100, -100);
            Vector3 minBounds = new Vector3(100, 100, 100);

            // alignment transforms from our desired rotation to the local coordinates, so inverse needed
            Matrix4x4 rotation = Matrix4x4.TRS(Vector3.zero, Quaternion.Inverse(alignment), Vector3.one);
            Matrix4x4 base_matrix = rotation * p.transform.worldToLocalMatrix;

            foreach (Transform t in p.FindModelComponents<Transform>())         //Get the max boundaries of the part
            {
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.sharedMesh;

                if (m == null)
                    continue;

                Matrix4x4 matrix = base_matrix * t.transform.localToWorldMatrix;

                foreach (Vector3 vertex in m.vertices)
                {
                    Vector3 v = matrix.MultiplyPoint3x4(vertex);

                    maxBounds.x = Mathf.Max(maxBounds.x, v.x);
                    minBounds.x = Mathf.Min(minBounds.x, v.x);
                    maxBounds.y = Mathf.Max(maxBounds.y, v.y);
                    minBounds.y = Mathf.Min(minBounds.y, v.y);
                    maxBounds.z = Mathf.Max(maxBounds.z, v.z);
                    minBounds.z = Mathf.Min(minBounds.z, v.z);
                }
            }

            if (maxBounds == new Vector3(-100, -100, -100) && minBounds == new Vector3(100, 100, 100))
            {
                Debug.LogWarning("[KJR] extents could not be properly built for part " + p.partInfo.title);
                maxBounds = minBounds = Vector3.zero;
            }
            else if (settings.debug)
                Debug.Log($"[KJR] Extents: {minBounds} .. {maxBounds} = {maxBounds - minBounds}");

            //attachNodeLoc = p.transform.worldToLocalMatrix.MultiplyVector(p.parent.transform.position - p.transform.position);
            return maxBounds - minBounds;
        }

        public static float CalculateRadius(Part p, Vector3 attachNodeLoc)
        {
            // y along attachNodeLoc; x,z orthogonal
            Vector3 maxExtents = CalculateExtents(p, attachNodeLoc);

            // Equivalent radius of an ellipse painted into the rectangle
            float radius = Mathf.Sqrt(maxExtents.x * maxExtents.z) / 2;

            return radius;
        }

        public static float CalculateSideArea(Part p, Vector3 attachNodeLoc)
        {
            Vector3 maxExtents = CalculateExtents(p, attachNodeLoc);
            //maxExtents = Vector3.Exclude(maxExtents, Vector3.up);

            float area = maxExtents.x * maxExtents.z;

            return area;
        }
    }
}
