using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalJointReinforcement
{
    public class KJRGroundJointModule : PartModule
    {
        private const int PartsToPick = 3;

        private List<Part> pickedMassiveParts = new List<Part>();
        private List<Part> clampParts;
        private Dictionary<Part, FixedJoint> joints = new Dictionary<Part, FixedJoint>();
        private bool alreadyUnpacked = false;
        private int partCount;
        private bool subscribedToVesselModifEvent = false;

        public void Init(List<Part> clampParts)
        {
            this.clampParts = clampParts;

            GameEvents.onPartDie.Add(OnPartDie);

            Part[] orderedParts = vessel.parts.OrderByDescending(p => p.physicsMass).ToArray();
            int i = 0;
            int uniquePartsPicked = 0;
            while (uniquePartsPicked < PartsToPick && i < orderedParts.Length)
            {
                Part mPart = orderedParts[i];
                i++;

                if (mPart.physicalSignificance == Part.PhysicalSignificance.NONE ||
                    clampParts.Contains(mPart) || pickedMassiveParts.Contains(mPart))
                    continue;

                pickedMassiveParts.Add(mPart);
                uniquePartsPicked++;

                // Add all symmetry counterparts too but they do not count towards the number of unique parts that should get selected
                if (mPart.symmetryCounterparts.Count > 0)
                {
                    pickedMassiveParts.AddRange(mPart.symmetryCounterparts);
                }
            }
        }

        public void OnPartUnpack()
        {
            if (alreadyUnpacked)
                return;

            alreadyUnpacked = true;
            partCount = vessel.parts.Count;

            StartCoroutine(CreateJointsOncePhysicsKicksIn());
        }

        private IEnumerator CreateJointsOncePhysicsKicksIn()
        {
            const int maxFrames = 1000;
            int i = 0;
            while (i++ < maxFrames && vessel.HoldPhysics)
            {
                if (KJRJointUtils.settings.debug)
                    Debug.Log($"[KJR] GroundJointModule: wait frame {i}");
                yield return new WaitForFixedUpdate();
            }

            if (vessel.HoldPhysics)
            {
                Debug.LogWarning($"[KJR] GroundJointModule: reached max wait of {maxFrames} frames but phys hold is still active");
                part.RemoveModule(this);
                yield break;
            }

            if (partCount != vessel.parts.Count)
            {
                Debug.Log($"[KJR] GroundJointModule: part count changed: {partCount} != {vessel.parts.Count}");
                part.RemoveModule(this);
                yield break;
            }

            foreach (Part part in pickedMassiveParts)
            {
                if (part == null) continue;

                FixedJoint newJoint = part.gameObject.AddComponent<FixedJoint>();
                joints.Add(part, newJoint);

                newJoint.connectedBody = null;
                newJoint.anchor = Vector3.zero;
                newJoint.axis = Vector3.up;
                newJoint.breakForce = Mathf.Infinity;
                newJoint.breakTorque = Mathf.Infinity;

                if (KJRJointUtils.settings.debug)
                    Debug.Log($"[KJR] {part.partInfo.title} connected to ground");
            }

            if (joints.Count > 0)
            {
                GameEvents.onVesselWasModified.Add(OnVesselWasModified);
                subscribedToVesselModifEvent = true;
            }
            else
            {
                part.RemoveModule(this);
            }
        }

        private void OnVesselWasModified(Vessel v)
        {
            if (v == vessel)
                BreakAllInvalidJoints();
        }

        private void OnPartDie(Part p)
        {
            if (clampParts.Contains(p))
            {
                clampParts.Remove(p);
                BreakAllInvalidJoints();
            }
        }

        private void BreakAllInvalidJoints()
        {
            foreach (Part key in joints.Keys.ToArray())
            {
                if (key == null || !clampParts.Find(cp => cp != null && cp.vessel == key.vessel))
                {
                    // All clamps gone, remove the joint
                    Destroy(joints[key]);
                    joints.Remove(key);
                }
            }

            if (joints.Count == 0)
                part.RemoveModule(this);
        }

        public void OnPartPack()
        {
            if (subscribedToVesselModifEvent)
            {
                GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
                subscribedToVesselModifEvent = false;
            }

            foreach (FixedJoint j in joints.Values)
                Destroy(j);

            joints.Clear();
            alreadyUnpacked = false;
        }

        public void OnDestroy()
        {
            if (subscribedToVesselModifEvent)
            {
                GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
            }

            GameEvents.onPartDie.Remove(OnPartDie);

            foreach (FixedJoint j in joints.Values)
                Destroy(j);

            joints.Clear();
        }
    }
}
