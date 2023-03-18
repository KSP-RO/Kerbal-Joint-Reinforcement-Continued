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

using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KerbalJointReinforcement
{
    //This class adds an extra joint between a launch clamp and the part it is connected to for stiffness
    public class KJRLaunchClampReinforcementModule : PartModule
    {
        public bool clampJointHasInfiniteStrength = false;

        private bool createHackedJoints = false;
        private bool subscribedToEvents = false;
        private List<ConfigurableJoint> joints;
        private List<ConfigurableJoint> hackedJoints;
        private List<Part> neighbours = new List<Part>();

        public override void OnAwake()
        {
            base.OnAwake();
            joints = new List<ConfigurableJoint>();
            hackedJoints = new List<ConfigurableJoint>();
        }

        public void OnPartUnpack()
        {
            if (part.parent == null)
                return;

            neighbours.Add(part.parent);

            StringBuilder debugString = null;
            if (KJRJointUtils.settings.debug)
            {
                debugString = new StringBuilder();
                debugString.AppendLine($"The following joints added by {part.partInfo.title} to increase stiffness:");
            }

            if (part.parent.Rigidbody != null)
            {
                if (clampJointHasInfiniteStrength)
                {
                    createHackedJoints = true;
                    StartCoroutine(CreateInfiniteStrengthJointRoutine());
                }
                else
                {
                    StrutConnectParts(part, part.parent);
                }
            }

            if (KJRJointUtils.settings.debug)
            {
                debugString.AppendLine($"{part.parent.partInfo.title} connected to part {part.partInfo.title}");
                Debug.Log(debugString.ToString());
            }

            if (joints.Count > 0 || createHackedJoints)
            {
                GameEvents.onVesselWasModified.Add(OnVesselWasModified);
                subscribedToEvents = true;
            }
        }

        private void OnVesselWasModified(Vessel v)
        {
            // When cheating a vessel into orbit, checking whether the clamp is on a different vessel (decoupled) does not work.
            // So hacked joints need to be destroyed without the checks below, otherwise an extremely nasty Kraken attack will ensue.
            foreach (ConfigurableJoint hj in hackedJoints)
                GameObject.Destroy(hj);

            hackedJoints.Clear();
            createHackedJoints = false;

            foreach (Part p in neighbours)
            {
                if (p.vessel == part.vessel)
                    continue;

                if (KJRJointUtils.settings.debug)
                    Debug.Log($"[KJR] Decoupling part {part.partInfo.title}; destroying all extra joints");
                
                BreakAllInvalidJoints();
                return;
            }
        }

        public void OnPartPack()
        {
            if (subscribedToEvents)
            {
                GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
                subscribedToEvents = false;
            }

            foreach (ConfigurableJoint j in joints)
                GameObject.Destroy(j);

            foreach (ConfigurableJoint hj in hackedJoints)
                GameObject.Destroy(hj);

            joints.Clear();
            hackedJoints.Clear();
            neighbours.Clear();
            createHackedJoints = false;
        }

        public void OnDestroy()
        {
            if (subscribedToEvents)
            {
                GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
                subscribedToEvents = false;
            }
        }

        private void BreakAllInvalidJoints()
        {
            GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
            subscribedToEvents = false;

            foreach (ConfigurableJoint j in joints)
                GameObject.Destroy(j);

            joints.Clear();

            var vessels = new List<Vessel>();

            foreach (Part n in neighbours)
            {
                if (n.vessel == null || vessels.Contains(n.vessel))
                    continue;

                vessels.Add(n.vessel);

                foreach (Part p in n.vessel.Parts)
                {
                    if (p.Modules.Contains<LaunchClamp>())
                        continue;

                    ConfigurableJoint[] possibleConnections = p.GetComponents<ConfigurableJoint>();

                    if (possibleConnections == null)
                        continue;

                    foreach (ConfigurableJoint j in possibleConnections)
                    {
                        if (j.connectedBody == null)
                        {
                            GameObject.Destroy(j);
                            continue;
                        }
                        Part cp = j.connectedBody.GetComponent<Part>();
                        if (cp != null && cp.vessel != p.vessel)
                            GameObject.Destroy(j);
                    }
                }
            }
            neighbours.Clear();
        }

        private void StrutConnectParts(Part partWithJoint, Part partConnectedByJoint)
        {
            Rigidbody rigidBody = partConnectedByJoint.rb;
            float breakForce = KJRJointUtils.settings.decouplerAndClampJointStrength;
            float breakTorque = KJRJointUtils.settings.decouplerAndClampJointStrength;
            Vector3 anchor, axis;
            anchor = Vector3.zero;
            axis = Vector3.right;

            ConfigurableJoint newJoint = partWithJoint.gameObject.AddComponent<ConfigurableJoint>();

            newJoint.connectedBody = rigidBody;
            newJoint.anchor = anchor;
            newJoint.axis = axis;
            newJoint.secondaryAxis = Vector3.forward;
            newJoint.breakForce = breakForce;
            newJoint.breakTorque = breakTorque;

            newJoint.xMotion = newJoint.yMotion = newJoint.zMotion = ConfigurableJointMotion.Locked;
            newJoint.angularXMotion = newJoint.angularYMotion = newJoint.angularZMotion = ConfigurableJointMotion.Locked;

            joints.Add(newJoint);
        }

        private IEnumerator CreateInfiniteStrengthJointRoutine()
        {
            const int maxFramesWaited = 250;
            int i = 0;
            do
            {
                yield return new WaitForFixedUpdate();
            } while (part.vessel.packed && i++ < maxFramesWaited);

            if (part.parent != null && part.parent.Rigidbody != null)
            {
                CreateInfiniteStrengthJoint(part.parent);
            }
        }

        private void CreateInfiniteStrengthJoint(Part parentOfClamp)
        {
            Vector3 anchor, axis;
            anchor = Vector3.zero;
            axis = Vector3.right;

            // Adding a joint that connects the parent part to itself. 
            // This causes Unity to bug out slightly and the connection between clamp and it's parent becomes completely rigid.
            Debug.Log("[KJR] Ignore the illegal joint error below, this is supposed to happen: ");
            ConfigurableJoint newJoint = parentOfClamp.gameObject.AddComponent<ConfigurableJoint>();
            newJoint.connectedBody = parentOfClamp.rb;
            newJoint.anchor = anchor;
            newJoint.axis = axis;
            newJoint.secondaryAxis = Vector3.forward;
            newJoint.breakForce = Mathf.Infinity;
            newJoint.breakTorque = Mathf.Infinity;

            newJoint.xMotion = newJoint.yMotion = newJoint.zMotion = ConfigurableJointMotion.Locked;
            newJoint.angularXMotion = newJoint.angularYMotion = newJoint.angularZMotion = ConfigurableJointMotion.Locked;
            newJoint.projectionMode = JointProjectionMode.PositionAndRotation;

            hackedJoints.Add(newJoint);
        }
    }
}
