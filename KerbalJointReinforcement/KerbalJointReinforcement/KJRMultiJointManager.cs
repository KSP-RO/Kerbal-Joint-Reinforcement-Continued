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
using UnityEngine;

namespace KerbalJointReinforcement
{
    //All this class exists to do is to act as a box attached to 
    //For a sequence of three parts (A, B, C), connected in series, this will exist on B and hold the strengthening joint from A to C
    //If the joint from A to B or B to C is broken, this will destroy the joint A to C and then destroy itself
    class KJRMultiJointManager
    {
        internal Dictionary<Part, List<ConfigurableJoint>> multiJointDict;
        private List<Part> linkPart1List;
        private List<Part> linkPart2List;
        private HashSet<Part> linkedSet;
        //Destroy() only removes a joint at the end of the frame. Joints waiting for that are kept here so they do not count as existing joints in the meantime.
        private HashSet<ConfigurableJoint> destroyedJoints;

        public KJRMultiJointManager()
        {
            multiJointDict = new Dictionary<Part, List<ConfigurableJoint>>();
            linkPart1List = new List<Part>();
            linkPart2List = new List<Part>();
            linkedSet = new HashSet<Part>();
            destroyedJoints = new HashSet<ConfigurableJoint>();
            GameEvents.onVesselCreate.Add(VesselCreate);
            GameEvents.onPartUndock.Add(OnJointBreak);
            GameEvents.onPartDie.Add(OnJointBreak);
        }

        public void OnDestroy()
        {
            Debug.Log("[KJR] KJRMultiJointManager cleanup");
            GameEvents.onVesselCreate.Remove(VesselCreate);
            GameEvents.onPartUndock.Remove(OnJointBreak);
            GameEvents.onPartDie.Remove(OnJointBreak);
        }
        
        //This entire scheme relies on a simple fact: when a vessel is created, the part that was decoupled is the root part
        //Therefore, we only need to use vessel.RootPart and the parts with no children to ensure that all multijoints are broken
        private void VesselCreate(Vessel v)
        {
            if (multiJointDict.Count == 0) return;

            //Debug.Log($"[KJR] {v.name} joint break");
            OnJointBreak(v.rootPart);

            for (int i = 0; i < v.Parts.Count; ++i)
            {
                Part p = v.Parts[i];
                OnJointBreak(p);
            }
        }

        public bool TrySetValidLinkedSet(Part linkPart1, Part linkPart2)
        {
            linkPart1List.Clear();
            linkPart2List.Clear();
            linkedSet.Clear();

            if (!KJRJointUtils.IsJointAdjustmentValid(linkPart1) || !KJRJointUtils.IsJointAdjustmentValid(linkPart2))
                return false;

            //Add the parts we're connecting to their respective lists
            linkPart1List.Add(linkPart1);
            linkPart2List.Add(linkPart2);

            //iterate through each part's parents, making them the new linkPart and then adding them to their respective lists.  LinkPart.parent will not be null until it reaches the root part
            while (!(linkPart1.parent is null))
            {
                linkPart1 = linkPart1.parent;
                linkPart1List.Add(linkPart1);
            }
            while (!(linkPart2.parent is null))
            {
                linkPart2 = linkPart2.parent;
                linkPart2List.Add(linkPart2);
            }

            //Due to the way the lists were created, index 0 = connected part, while index = Count - 1 = root part
            int index1 = linkPart1List.Count - 1;
            int index2 = linkPart2List.Count - 1;

            //Parts that do not share a root are not in the same part tree and cannot be linked
            if (linkPart1List[index1] != linkPart2List[index2])
                return false;

            //Walk down from the root while both lines still go through the same part. The last shared part is where the lines merge.
            while (index1 > 0 && index2 > 0 && linkPart1List[index1 - 1] == linkPart2List[index2 - 1])
            {
                index1--;
                index2--;
            }

            //Only the parts from each connected part up to and including the merge part are crossed by the joint.
            //Parts above the merge part move together with both connected parts, so they do not affect the joint.
            for (int i = 0; i <= index1; i++)
            {
                Part p = linkPart1List[i];
                if (!KJRJointUtils.IsJointAdjustmentValid(p))
                    return false;
                linkedSet.Add(p);
            }

            for (int i = 0; i <= index2; i++)
            {
                Part q = linkPart2List[i];
                if (!KJRJointUtils.IsJointAdjustmentValid(q))
                    return false;
                linkedSet.Add(q);
            }

            return true;
        }

        public void RegisterMultiJointBetweenParts(Part linkPart1, Part linkPart2, ConfigurableJoint multiJoint)
        {
            foreach (Part p in linkedSet)
                RegisterMultiJoint(p, multiJoint);
        }

        public void RegisterMultiJoint(Part testPart, ConfigurableJoint multiJoint)
        {
            List<ConfigurableJoint> configJointList;
            if (multiJointDict.TryGetValue(testPart, out configJointList))
            {
                bool alreadyExists = false;
                for (int i = configJointList.Count - 1; i >= 0; --i)
                {
                    ConfigurableJoint joint = configJointList[i];
                    if (IsJointGone(joint))
                        configJointList.RemoveAt(i);

                    alreadyExists |= joint == multiJoint;
                }

                if (!alreadyExists)
                    configJointList.Add(multiJoint);
            }
            else
            {
                configJointList = new List<ConfigurableJoint>();
                configJointList.Add(multiJoint);
                multiJointDict.Add(testPart, configJointList);
            }
        }

        public bool CheckMultiJointBetweenParts(Part testPart1, Part testPart2)
        {
            if (testPart1 == null || testPart2 == null || testPart1 == testPart2)
                return false;

            List<ConfigurableJoint> testMultiJoints;

            if (!multiJointDict.TryGetValue(testPart1, out testMultiJoints))
                return false;

            Rigidbody testRb = testPart2.rb;

            for (int i = 0; i < testMultiJoints.Count; i++)
            {
                ConfigurableJoint joint = testMultiJoints[i];
                if (IsJointGone(joint))
                {
                    testMultiJoints.RemoveAt(i);
                    --i;
                    continue;
                }
                if (joint.connectedBody == testRb || joint.gameObject == testPart2.gameObject)
                    return true;
            }

            return false;
        }

        public void OnJointBreak(Part part)
        {
            if (part == null)
                return;
            List<ConfigurableJoint> configJointList;
            if (multiJointDict.TryGetValue(part, out configJointList))
            {
                for(int i = 0; i < configJointList.Count; i++)
                {
                    ConfigurableJoint joint = configJointList[i];
                    if (!IsJointGone(joint))
                    {
                        GameObject.Destroy(joint);
                        destroyedJoints.Add(joint);
                    }
                }

                multiJointDict.Remove(part);
            }
        }

        public void ClearTempLists()
        {
            linkPart1List.Clear();
            linkPart2List.Clear();
            linkedSet.Clear();

            //Joints that Unity has finished destroying compare equal to null and no longer need to be tracked
            destroyedJoints.RemoveWhere(j => j == null);
        }

        private bool IsJointGone(ConfigurableJoint joint)
        {
            return joint == null || destroyedJoints.Contains(joint);
        }
    }
}
