// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Copy Paste", "Reneb", 2.1)]
    class CopyPaste : RustPlugin
    {
        private MethodInfo CreateEntity;
        private MethodInfo FindPrefab;
        private FieldInfo serverinput;


        void Loaded() 
        {
            //CreateEntity = typeof(GameManager).GetMethod("CreateEntity");
            //FindPrefab = typeof(GameManager).GetMethod("FindPrefab");
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        }
        void GetClosestRayPoint(Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint)
        {
            Vector3 sourceEye = sourcePos + new Vector3(0f, 1.5f, 0f);
            UnityEngine.Ray ray = new UnityEngine.Ray(sourceEye, sourceDir * Vector3.forward);

            var hits = UnityEngine.Physics.RaycastAll(ray);
            float closestdist = 999999f;
            closestHitpoint = sourcePos;
            closestEnt = false;
            foreach (var hit in hits)
            {
                    if (hit.distance < closestdist)
                    {
                        closestdist = hit.distance;
                        closestEnt = hit.collider;
                        closestHitpoint = hit.point;
                    }
            }
        }
        Vector3 GenerateGoodPos(Vector3 Pos, float diffRot)
        {
            float newX = (Pos.x * (float)Math.Cos(diffRot)) + (Pos.z * (float)Math.Sin(diffRot));
            float newZ = (Pos.z * (float)Math.Cos(diffRot)) - (Pos.x * (float)Math.Sin(diffRot));
            Pos.x = newX;
            Pos.z = newZ;
            return Pos;
        }
        object GetStructureClean(BuildingBlock initialBlock, float playerRot, BuildingBlock currentBlock)
        {
            var data = new Dictionary<string, object>();
            float normRotY = currentBlock.transform.rotation.ToEulerAngles().y - playerRot;
            var transformedPos = currentBlock.transform.position - initialBlock.transform.position;
            var normedPos = GenerateGoodPos(transformedPos, -playerRot);
            data.Add("prefabname",currentBlock.blockDefinition.fullname);
            data.Add("grade",currentBlock.grade);
            var PosData = new Dictionary<string, object>();
            PosData.Add("x",normedPos.x);
            PosData.Add("y",normedPos.y);
            PosData.Add("z",normedPos.z);
            data.Add("pos", PosData);
            var RotData = new Dictionary<string, object>();
            RotData.Add("x", currentBlock.transform.rotation.ToEulerAngles().x);
            RotData.Add("y",normRotY);
            RotData.Add("z", currentBlock.transform.rotation.ToEulerAngles().z);
            data.Add("rot", RotData);
            return data;
        }
        /*object GetBoxClean(BuildingBlock initialBlock, float playerRot, ProtoBuf.StorageBox currentBlock)
        {
            var data = new Dictionary<string, object>();
            float normRotY = currentBlock.transform.rotation.ToEulerAngles().y - playerRot;
            var transformedPos = currentBlock.transform.position - initialBlock.transform.position;
            var normedPos = GenerateGoodPos(transformedPos, -playerRot);
            data.Add("prefabname", currentBlock.item.info.shortname);
            var PosData = new Dictionary<string, float>();
            PosData.Add("x", normedPos.x);
            PosData.Add("y", normedPos.y);
            PosData.Add("z", normedPos.z);
            data.Add("pos", PosData);
            var RotData = new Dictionary<string, float>();
            RotData.Add("x", currentBlock.transform.rotation.ToEulerAngles().x);
            RotData.Add("y", normRotY);
            RotData.Add("z", currentBlock.transform.rotation.ToEulerAngles().z);
            data.Add("rot", RotData);
            return data;
        }*/
        object GetDeployedClean(BuildingBlock initialBlock, float playerRot, DeployedItem currentBlock)
        {
            var data = new Dictionary<string, object>();
            float normRotY = currentBlock.transform.rotation.ToEulerAngles().y - playerRot;
            var transformedPos = currentBlock.transform.position - initialBlock.transform.position;
            var normedPos = GenerateGoodPos(transformedPos, -playerRot);
            data.Add("prefabname", currentBlock.item.info.shortname);
            data.Add("amount", currentBlock.item.amount);
            var PosData = new Dictionary<string, object>();
            PosData.Add("x", normedPos.x);
            PosData.Add("y", normedPos.y);
            PosData.Add("z", normedPos.z);
            data.Add("pos", PosData);
            var RotData = new Dictionary<string, object>();
            RotData.Add("x", currentBlock.transform.rotation.ToEulerAngles().x);
            RotData.Add("y", normRotY);
            RotData.Add("z", currentBlock.transform.rotation.ToEulerAngles().z);
            data.Add("rot", RotData);
            return data;
        }
        void CopyBuilding(Vector3 playerPos, float playerRot, BuildingBlock initialBlock, out List<object> rawStructure, out List<object> rawDeployables, out List<object> rawStorages)
        {
            rawStructure = new List<object>();
            rawDeployables = new List<object>();
            rawStorages = new List<object>();
            List<BuildingBlock> houseList = new List<BuildingBlock>();
            List<DeployedItem> deployList = new List<DeployedItem>();
            List<ProtoBuf.StorageBox> storageBoxList = new List<ProtoBuf.StorageBox>();
            List<Vector3> checkFrom = new List<Vector3>();
            houseList.Add(initialBlock);
            checkFrom.Add(initialBlock.transform.position);
            {
                var housedata = GetStructureClean(initialBlock, playerRot, initialBlock);
                rawStructure.Add(housedata);
            }
            int current = 0;
            while (true)
            {
                current++;
                if (current > checkFrom.Count)
                    break;
                var hits = UnityEngine.Physics.OverlapSphere(checkFrom[current - 1], 3f);
                foreach (var hit in hits)
                {
                    if (hit.GetComponentInParent<BuildingBlock>() != null)
                    {
                        BuildingBlock fbuildingblock = hit.GetComponentInParent<BuildingBlock>();
                        if (!(houseList.Contains(fbuildingblock)))
                        {
                            houseList.Add(fbuildingblock);
                            checkFrom.Add(fbuildingblock.transform.position);
                            var housedata = GetStructureClean(initialBlock, playerRot, fbuildingblock);
                            rawStructure.Add(housedata);
                        }
                    }
                    else if (hit.GetComponentInParent<DeployedItem>() != null)
                    {
                        DeployedItem deployeditem = hit.GetComponentInParent<DeployedItem>();
                        if (!(deployList.Contains(deployeditem)))
                        {
                            deployList.Add(deployeditem);
                            var deploydata = GetDeployedClean(initialBlock, playerRot, deployeditem);
                            rawDeployables.Add(deploydata);
                        }
                    }
                    /*else if (hit.GetComponentInParent<ProtoBuf.StorageBox>() != null)
                    {
                        ProtoBuf.StorageBox storageBox = hit.GetComponentInParent<ProtoBuf.StorageBox>();
                        if (!(storageBoxList.Contains(storageBox)))
                        {
                            storageBoxList.Add(storageBox);
                            //var deploydata = GetBoxClean(initialBlock, playerRot, storageBox);
                            //rawStorages.Add(deploydata);
                        }
                    }*/
                }
            }
        }
        [ChatCommand("copy")]
        void cmdChatCopy(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 1)
            {
                SendReply(player, "You are not allowed to use this command");
                return;
            }
            if (args == null || args.Length == 0)
            {
                SendReply(player, "You need to set the name of the copy file: /copy NAME");
                return;
            }
            string filename = string.Format("copypaste-{0}", args[0].ToString());

            var input = serverinput.GetValue(player) as InputState;
            Quaternion currentRot = Quaternion.Euler(input.current.aimAngles);
            object closestEnt;
            Vector3 closestHitpoint;
            GetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint);
            if (closestEnt is bool)
            {
                SendReply(player, "Couldn't find any Entity");
                return;
            }
            var baseentity = closestEnt as Collider;
            if (baseentity == null)
            {
                SendReply(player, "You are not looking at a Structure, or something is blocking the view.");
                return;
            }
            var buildingblock = baseentity.GetComponentInParent<BuildingBlock>();
            if (buildingblock == null)
            {
                SendReply(player, "You are not looking at a Structure, or something is blocking the view.");
                return;
            }
            List<object> rawStructure = new List<object>();
            List<object> rawDeployables = new List<object>();
            List<object> rawStorages = new List<object>();
            CopyBuilding(player.transform.position, currentRot.ToEulerAngles().y, buildingblock, out rawStructure, out rawDeployables, out rawStorages);
            if (rawStructure.Count == 0)
            {
                SendReply(player, "Something went wrong, house is empty?");
                return;
            }
            Dictionary<string, object> defaultValues = new Dictionary<string, object>();
            Dictionary<string, object> defaultPos = new Dictionary<string, object>();
            defaultPos.Add("x", buildingblock.transform.position.x);
            defaultPos.Add("y", buildingblock.transform.position.y);
            defaultPos.Add("z", buildingblock.transform.position.z);
            defaultValues.Add("position", defaultPos);
            defaultValues.Add("yrotation", buildingblock.transform.rotation.ToEulerAngles().y);

            Core.Configuration.DynamicConfigFile CopyData = Interface.GetMod().DataFileSystem.GetDatafile(filename);
            CopyData.Clear();
            CopyData["structure"] = rawStructure;
            CopyData["deployables"] = rawDeployables;
            CopyData["default"] = defaultValues;
            Interface.GetMod().DataFileSystem.SaveDatafile(filename);

            SendReply(player,string.Format("The house {0} was successfully saved",args[0].ToString()));
            SendReply(player,string.Format("{0} building parts detected",rawStructure.Count.ToString()));
            SendReply(player,string.Format("{0} deployables detected",rawDeployables.Count.ToString()));
        }
        [ChatCommand("paste")]
        void cmdChatPaste(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 1)
            {
                SendReply(player, "You are not allowed to use this command");
                return;
            }
            if (args == null || args.Length == 0)
            {
                SendReply(player, "You need to set the name of the copy file: /paste NAME optional:HeightAdjustment");
                return;
            }
            float heightAdjustment = 0.5f;
            if (args.Length > 1)
            {
                float.TryParse(args[1].ToString(), out heightAdjustment);
            }
            string filename = string.Format("copypaste-{0}", args[0].ToString());

            var input = serverinput.GetValue(player) as InputState;
            Quaternion currentRot = Quaternion.Euler(input.current.aimAngles);
            object closestEnt;
            Vector3 closestHitpoint;
            GetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint);
            if (closestEnt is bool)
            {
                SendReply(player, "Couldn't find any Entity");
                return;
            }
            var baseentity = closestEnt as Collider;
            if (baseentity == null)
            {
                SendReply(player, "You are not looking at a Structure, or something is blocking the view.");
                return;
            }
            closestHitpoint.y = closestHitpoint.y + heightAdjustment;
            Core.Configuration.DynamicConfigFile PasteData = Interface.GetMod().DataFileSystem.GetDatafile(filename);
            if (PasteData["structure"] == null || PasteData["default"] == null)
            {
                SendReply(player, "This is not a correct copypaste file, or it's empty.");
                return;
            }
            List<object> structureData = PasteData["structure"] as List<object>;
            bool stability = server.stability;
            server.stability = false;
            PasteBuilding(structureData, closestHitpoint, currentRot.ToEulerAngles().y, heightAdjustment);
            if (PasteData["deployables"] != null )
            {
                List<object> deployedData = PasteData["deployables"] as List<object>;
                if (deployedData.Count > 0)
                {
                    PasteDeployables(player, deployedData, closestHitpoint, currentRot.ToEulerAngles().y, heightAdjustment);
                }
            }
        }
        [ChatCommand("placeback")]
        void cmdChatPlaceback(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 1)
            {
                SendReply(player, "You are not allowed to use this command");
                return;
            }
            if (args == null || args.Length == 0)
            {
                SendReply(player, "You need to set the name of the copy file: /placeback NAME");
                return;
            }
            float heightAdjustment = 0;
            string filename = string.Format("copypaste-{0}", args[0].ToString());

            Core.Configuration.DynamicConfigFile PasteData = Interface.GetMod().DataFileSystem.GetDatafile(filename);
            if (PasteData["structure"] == null || PasteData["default"] == null)
            {
                SendReply(player, "This is not a correct copypaste file, or it's empty.");
                return;
            }
            Dictionary<string,object> defaultData = PasteData["default"] as Dictionary<string,object>;
            Dictionary<string, object> defaultPos = defaultData["position"] as Dictionary<string, object>;
            Vector3 defaultposition = new Vector3(Convert.ToSingle(defaultPos["x"]), Convert.ToSingle(defaultPos["y"]), Convert.ToSingle(defaultPos["z"]));
            List<object> structureData = PasteData["structure"] as List<object>;
            bool stability = server.stability;
            server.stability = false;
            PasteBuilding(structureData, defaultposition, Convert.ToSingle(defaultData["yrotation"]), heightAdjustment);
            if (PasteData["deployables"] != null)
            {
                List<object> deployedData = PasteData["deployables"] as List<object>;
                if (deployedData.Count > 0)
                {
                    PasteDeployables(player, deployedData, defaultposition, Convert.ToSingle(defaultData["yrotation"]), heightAdjustment);
                }
            }
        }
        void SpawnStructure(UnityEngine.GameObject prefab, Vector3 pos, Quaternion angles, int grade)
        {
            UnityEngine.GameObject build =  UnityEngine.Object.Instantiate( prefab );
            if(build == null) return;
            BuildingBlock block = build.GetComponent<BuildingBlock>();
            if(block == null) return;
            block.transform.position = pos;
            block.transform.rotation = angles;
            block.gameObject.SetActive(true);
            block.blockDefinition = Construction.Library.FindByPrefabID(block.prefabID);
            block.Spawn(true);
            block.SetGrade(grade);
            block.health = block.MaxHealth();
            
        }
        void PasteDeployables(BasePlayer player, List<object> deployerData, Vector3 targetPoint, float targetRot, float heightAdjustment)
        {
            Vector3 OriginRotation = new Vector3(0f, targetRot, 0f);
            Quaternion OriginRot = Quaternion.EulerRotation(OriginRotation);
            foreach (Dictionary<string, object> structure in deployerData)
            {
                Dictionary<string, object> structPos = structure["pos"] as Dictionary<string, object>;
                Dictionary<string, object> structRot = structure["rot"] as Dictionary<string, object>;
                string prefabname = (string)structure["prefabname"];
                int amount = (int)structure["amount"];
                Quaternion newAngles = Quaternion.EulerRotation((new Vector3(Convert.ToSingle(structRot["x"]), Convert.ToSingle(structRot["y"]), Convert.ToSingle(structRot["z"]))) + OriginRotation);
                Vector3 TempPos = OriginRot * (new Vector3(Convert.ToSingle(structPos["x"]), Convert.ToSingle(structPos["y"]), Convert.ToSingle(structPos["z"])));
                Vector3 NewPos = TempPos + targetPoint;
                Item newItem = ItemManager.CreateByName(prefabname, 1);
                if (newItem != null)
                {
                    var deployer = newItem.info.FindModule<Item.Modules.Deploy>();
                    if(deployer != null)
                    {
                        BaseEntity ent = GameManager.server.CreateEntity(deployer.deployablePrefabName, NewPos, newAngles);
                        if(ent != null)
                        {
                            ent.SendMessage("SetDeployedBy", player, UnityEngine.SendMessageOptions.DontRequireReceiver);
                            ent.SendMessage("InitializeItem", newItem, UnityEngine.SendMessageOptions.DontRequireReceiver);
                            ent.Spawn(true);
                            newItem.SetWorldEntity(ent);
                            newItem.SetHeldEntity(null);
                        }
                    }
                }
            }
        }


        void PasteBuilding(List<object> structureData, Vector3 targetPoint, float targetRot, float heightAdjustment)
        {
            Vector3 OriginRotation = new Vector3(0f, targetRot, 0f);
            Quaternion OriginRot = Quaternion.EulerRotation( OriginRotation );
            foreach (Dictionary<string, object> structure in structureData)
            {
                
                Dictionary<string, object> structPos = structure["pos"] as Dictionary<string, object>;
                Dictionary<string, object> structRot = structure["rot"] as Dictionary<string, object>;
                string prefabname = (string)structure["prefabname"];
                int grade = (int)structure["grade"];
                Quaternion newAngles = Quaternion.EulerRotation((new Vector3(Convert.ToSingle(structRot["x"]), Convert.ToSingle(structRot["y"]), Convert.ToSingle(structRot["z"]))) + OriginRotation);
                Vector3 TempPos = OriginRot * (new Vector3(Convert.ToSingle(structPos["x"]),Convert.ToSingle(structPos["y"]), Convert.ToSingle(structPos["z"])));
                Vector3 NewPos = TempPos + targetPoint;
                UnityEngine.GameObject newPrefab = GameManager.server.FindPrefab(prefabname);
                if (newPrefab != null)
                {
                    SpawnStructure(newPrefab, NewPos, newAngles, grade);
                }
            }
        }
    }
}
