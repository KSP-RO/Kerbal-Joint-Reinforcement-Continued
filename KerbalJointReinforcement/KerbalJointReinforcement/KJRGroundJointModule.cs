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
        private bool subscribedToEvents = false;

        public void Init(List<Part> clampParts)
        {
            this.clampParts = clampParts;

            Part[] orderedParts = vessel.parts.OrderByDescending(p => p.physicsMass).ToArray();
            int i = 0;
            int uniquePartsPicked = 0;
            while (uniquePartsPicked < PartsToPick && i < orderedParts.Length)
            {
                Part mPart = orderedParts[i];
                i++;

                if (clampParts.Contains(mPart) || pickedMassiveParts.Contains(mPart))
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
                subscribedToEvents = true;

                // By the time the code gets here, the stock CheckGroundCollision() method has been run at least once.
                // With the extra world-space joints, it's safe to make the assumption that the vessel will
                // never shift in a significant way so any further collision checks aren't necessary.
                // This also means that stock code will no longer shift the vessel up or down when coming off rails.
                vessel.skipGroundPositioning = true;
            }
            else
            {
                part.RemoveModule(this);
            }
        }

        private void OnVesselWasModified(Vessel v)
        {
            BreakAllInvalidJoints();
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
            if (subscribedToEvents)
            {
                GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
                subscribedToEvents = false;
            }

            foreach (FixedJoint j in joints.Values)
                Destroy(j);

            joints.Clear();
            alreadyUnpacked = false;
        }

        public void OnDestroy()
        {
            if (subscribedToEvents)
            {
                GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
            }

            foreach (FixedJoint j in joints.Values)
                Destroy(j);

            joints.Clear();
        }
    }
}
