// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Copy Paste", "Reneb", "2.2.0")]
    class CopyPaste : RustPlugin
    {
        private MethodInfo CreateEntity;
        private MethodInfo FindPrefab;
        private FieldInfo serverinput;
        private Dictionary<string, string> deployedToItem;

        /// CACHED VARIABLES

        private Vector3 transformedPos;
        private Vector3 normedPos;
        private Quaternion currentRot;
        private float normedYRot;
        private float newX;
        private float newZ;
        private Dictionary<string, object> posCleanData;
        private Dictionary<string, object> rotCleanData;
        private List<object> rawStructure;
        private List<object> rawDeployables;
        private List<object> rawStorages;
        private float heightAdjustment;
        private string filename;
        private object closestEnt;
        private Vector3 closestHitpoint;
        private string cleanDeployedName;

        /////////////////////////////////////////////////////
        ///  OXIDE HOOKS
        /////////////////////////////////////////////////////

        /////////////////////////////////////////////////////
        ///  Loaded()
        ///  When the plugin is loaded by Oxide
        /////////////////////////////////////////////////////
        void Loaded() 
        {
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            deployedToItem = new Dictionary<string, string>();
        }
        void OnServerInitialized()
        {
            var allItemsDef = UnityEngine.Resources.FindObjectsOfTypeAll<ItemDefinition>();
            foreach (ItemDefinition itemDef in allItemsDef)
            {
                if (itemDef.GetComponent<ItemModDeployable>() != null )
                {
                    deployedToItem.Add(itemDef.GetComponent<ItemModDeployable>().entityPrefab.targetObject.gameObject.name.ToString(), itemDef.shortname.ToString());
                }
            }
        }

        /////////////////////////////////////////////////////
        ///  GENERAL FUNCTIONS
        /////////////////////////////////////////////////////

        /////////////////////////////////////////////////////
        ///  TryGetClosestRayPoint( Vector3, Quaternion, out object, out Vector3 )
        ///  Get the closest raypoint

        bool TryGetClosestRayPoint(Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint)
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
            if (closestEnt is bool)
                return false;
            return true;
        }

        bool hasAccess(BasePlayer player)
        {
            if (player.net.connection.authLevel < 1)
            {
                SendReply(player, "You are not allowed to use this command");
                return false;
            }
            return true;
        }
        bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
        {
            viewAngle = new Quaternion(0f,0f,0f,0f);
            var input = serverinput.GetValue(player) as InputState;
            if (input == null)
                return false;
            if (input.current == null)
                return false;
            if (input.current.aimAngles == null)
                return false;

            viewAngle = Quaternion.Euler(input.current.aimAngles);
            return true;
        }
        bool GetCleanDeployedName(string sourcename, out string name)
        {
            name = "";
            if(sourcename.IndexOf("(Clone)",0) > -1)
            {
                sourcename = sourcename.Substring(0,sourcename.IndexOf("(Clone)",0));
                if (deployedToItem.ContainsKey(sourcename))
                {
                    name = deployedToItem[sourcename];
                    return true;
                }
            }
            return false;
        } 
        Vector3 GenerateGoodPos(Vector3 InitialPos, Vector3 CurrentPos, float diffRot)
        {
            transformedPos = CurrentPos - InitialPos;
            newX = (transformedPos.x * (float)Math.Cos(-diffRot)) + (transformedPos.z * (float)Math.Sin(-diffRot));
            newZ = (transformedPos.z * (float)Math.Cos(-diffRot)) - (transformedPos.x * (float)Math.Sin(-diffRot));
            transformedPos.x = newX;
            transformedPos.z = newZ;
            return transformedPos;
        }

        bool GetStructureClean(BuildingBlock initialBlock, float playerRot, BuildingBlock currentBlock, out Dictionary<string, object> data)
        {
            data = new Dictionary<string, object>();
            posCleanData = new Dictionary<string, object>();
            rotCleanData = new Dictionary<string, object>();

            normedPos = GenerateGoodPos(initialBlock.transform.position, currentBlock.transform.position, playerRot);
            normedYRot = currentBlock.transform.rotation.ToEulerAngles().y - playerRot;
            
            data.Add("prefabname",currentBlock.blockDefinition.fullname);
            data.Add("grade",currentBlock.grade);

            posCleanData.Add("x", normedPos.x);
            posCleanData.Add("y", normedPos.y);
            posCleanData.Add("z", normedPos.z);
            data.Add("pos", posCleanData);

            rotCleanData.Add("x", currentBlock.transform.rotation.ToEulerAngles().x);
            rotCleanData.Add("y", normedYRot);
            rotCleanData.Add("z", currentBlock.transform.rotation.ToEulerAngles().z);
            data.Add("rot", rotCleanData);
            return true;
        }

        bool GetDeployableClean(BuildingBlock initialBlock, float playerRot, Deployable currentBlock, out Dictionary<string, object> data)
        {
            data = new Dictionary<string, object>();
            posCleanData = new Dictionary<string, object>();
            rotCleanData = new Dictionary<string, object>();

            normedPos = GenerateGoodPos(initialBlock.transform.position, currentBlock.transform.position, playerRot);
            normedYRot = currentBlock.transform.rotation.ToEulerAngles().y - playerRot;

            if(!GetCleanDeployedName(currentBlock.gameObject.name.ToString(), out cleanDeployedName))
                return false;
            data.Add("prefabname", cleanDeployedName);

            posCleanData.Add("x", normedPos.x);
            posCleanData.Add("y", normedPos.y);
            posCleanData.Add("z", normedPos.z);
            data.Add("pos", posCleanData);

            rotCleanData.Add("x", currentBlock.transform.rotation.ToEulerAngles().x);
            rotCleanData.Add("y", normedYRot);
            rotCleanData.Add("z", currentBlock.transform.rotation.ToEulerAngles().z);
            data.Add("rot", rotCleanData);
            return true;
        }

        object CopyBuilding(Vector3 playerPos, float playerRot, BuildingBlock initialBlock, out List<object> rawStructure, out List<object> rawDeployables, out List<object> rawStorages)
        {
            rawStructure = new List<object>();
            rawDeployables = new List<object>();
            rawStorages = new List<object>();
            List<object> houseList = new List<object>();
            List<Vector3> checkFrom = new List<Vector3>();
            BuildingBlock fbuildingblock;
            Deployable fdeployable;

            houseList.Add(initialBlock);
            checkFrom.Add(initialBlock.transform.position);

            Dictionary<string, object> housedata;
            if (!GetStructureClean(initialBlock, playerRot, initialBlock, out housedata))
            {
                return "Couldn't get a clean initial block";
            }
            rawStructure.Add(housedata);

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
                        fbuildingblock = hit.GetComponentInParent<BuildingBlock>();
                        if (!(houseList.Contains(fbuildingblock)))
                        {
                            houseList.Add(fbuildingblock);
                            checkFrom.Add(fbuildingblock.transform.position);
                            if (GetStructureClean(initialBlock, playerRot, fbuildingblock, out housedata))
                            {
                                rawStructure.Add(housedata);
                            }
                        }
                    }
                    else if (hit.GetComponentInParent<Deployable>() != null)
                    {
                        fdeployable = hit.GetComponentInParent<Deployable>();
                        if (!(houseList.Contains(fdeployable)))
                        {
                            houseList.Add(fdeployable);
                            if (GetDeployableClean(initialBlock, playerRot, fdeployable, out housedata))
                            {
                                rawDeployables.Add(housedata);
                            }
                        }
                    }
                }
            }
            return true;
        }

        /////////////////////////////////////////////////////
        ///  CHAT COMMANDS
        /////////////////////////////////////////////////////
        [ChatCommand("copy")]
        void cmdChatCopy(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;

            if (args == null || args.Length == 0)
            {
                SendReply(player, "You need to set the name of the copy file: /copy NAME");
                return;
            }
            
            // Get player camera view directly from the player
            if (!TryGetPlayerView(player, out currentRot))
            {
                SendReply(player, "Couldn't find your eyes");
                return;
            }

            // Get what the player is looking at
            if(!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint))
            {
                SendReply(player, "Couldn't find any Entity");
                return;
            }

            // Check if what the player is looking at is a collider
            var baseentity = closestEnt as Collider;
            if (baseentity == null)
            {
                SendReply(player, "You are not looking at a Structure, or something is blocking the view.");
                return;
            }

            // Check if what the player is looking at is a BuildingBlock (like a wall or something like that)
            var buildingblock = baseentity.GetComponentInParent<BuildingBlock>();
            if (buildingblock == null)
            {
                SendReply(player, "You are not looking at a Structure, or something is blocking the view.");
                return;
            }

            var returncopy = CopyBuilding(player.transform.position, currentRot.ToEulerAngles().y, buildingblock, out rawStructure, out rawDeployables, out rawStorages);
            if (returncopy is string)
            {
                SendReply(player, (string)returncopy);
                return;
            }

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

            filename = string.Format("copypaste-{0}", args[0].ToString());
            Core.Configuration.DynamicConfigFile CopyData = Interface.GetMod().DataFileSystem.GetDatafile(filename);
            CopyData.Clear();
            CopyData["structure"] = rawStructure;
            CopyData["deployables"] = rawDeployables;
            CopyData["default"] = defaultValues;


            Interface.GetMod().DataFileSystem.SaveDatafile(filename);

            SendReply(player,string.Format("The house {0} was successfully saved",args[0].ToString()));
            SendReply(player,string.Format("{0} building parts detected",rawStructure.Count.ToString()));
            SendReply(player, string.Format("{0} deployables detected", rawDeployables.Count.ToString()));
        }
        [ChatCommand("paste")]
        void cmdChatPaste(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (args == null || args.Length == 0)
            {
                SendReply(player, "You need to set the name of the copy file: /paste NAME optional:HeightAdjustment");
                return;
            }

            // Adjust height so you don't automatically paste in the ground
            heightAdjustment = 0.5f;
            if (args.Length > 1)
            {
                float.TryParse(args[1].ToString(), out heightAdjustment);
            }

            // Get player camera view directly from the player
            if (!TryGetPlayerView(player, out currentRot))
            {
                SendReply(player, "Couldn't find your eyes");
                return;
            }

            // Get what the player is looking at
            if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint))
            {
                SendReply(player, "Couldn't find any Entity");
                return;
            }

            // Check if what the player is looking at is a collider
            var baseentity = closestEnt as Collider;
            if (baseentity == null)
            {
                SendReply(player, "You are not looking at a Structure, or something is blocking the view.");
                return;
            }
            closestHitpoint.y = closestHitpoint.y + heightAdjustment;

            filename = string.Format("copypaste-{0}", args[0].ToString());
            Core.Configuration.DynamicConfigFile PasteData = Interface.GetMod().DataFileSystem.GetDatafile(filename);
            if (PasteData["structure"] == null || PasteData["default"] == null)
            {
                SendReply(player, "This is not a correct copypaste file, or it's empty.");
                return;
            }
            List<object> structureData = PasteData["structure"] as List<object>;
            List<object> deployablesData = PasteData["deployables"] as List<object>;

            PasteBuilding(structureData, closestHitpoint, currentRot.ToEulerAngles().y, heightAdjustment);
            PasteDeployables(deployablesData, closestHitpoint, currentRot.ToEulerAngles().y, heightAdjustment, player);
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
            heightAdjustment = 0;
            filename = string.Format("copypaste-{0}", args[0].ToString());

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
            List<object> deployablesData = PasteData["deployables"] as List<object>;

            PasteBuilding(structureData, defaultposition, Convert.ToSingle(defaultData["yrotation"]), heightAdjustment);
            PasteDeployables(deployablesData, defaultposition, Convert.ToSingle(defaultData["yrotation"]), heightAdjustment, player);
        }
        void SpawnStructure(UnityEngine.GameObject prefab, Vector3 pos, Quaternion angles, BuildingGrade.Enum grade)
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
        void SpawnDeployable(Item newitem, Vector3 pos, Quaternion angles, BasePlayer player)
        {
            if (newitem.info.GetComponent<ItemModDeployable>() == null)
            {
                SendReply(player,"1");
                return;
            }
            var deployable = newitem.info.GetComponent<ItemModDeployable>().entityPrefab.targetObject.GetComponent<Deployable>();
            if (deployable == null)
            {
                SendReply(player, "2");
                return;
            }
            var newBaseEntity = GameManager.server.CreateEntity( deployable.gameObject, pos, angles );
            if (newBaseEntity == null)
            {
                SendReply(player, "3");
                return;
            }
            newBaseEntity.SendMessage("SetDeployedBy", player, UnityEngine.SendMessageOptions.DontRequireReceiver );
            newBaseEntity.SendMessage("InitializeItem", newitem, UnityEngine.SendMessageOptions.DontRequireReceiver);
            newBaseEntity.Spawn(true);
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
                BuildingGrade.Enum grade = (BuildingGrade.Enum) structure["grade"];
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
        void PasteDeployables(List<object> deployablesData, Vector3 targetPoint, float targetRot, float heightAdjustment, BasePlayer player)
        {
            Vector3 OriginRotation = new Vector3(0f, targetRot, 0f);
            Quaternion OriginRot = Quaternion.EulerRotation(OriginRotation);
            foreach (Dictionary<string, object> deployable in deployablesData)
            {

                Dictionary<string, object> structPos = deployable["pos"] as Dictionary<string, object>;
                Dictionary<string, object> structRot = deployable["rot"] as Dictionary<string, object>;
                string prefabname = (string)deployable["prefabname"];
                Quaternion newAngles = Quaternion.EulerRotation((new Vector3(Convert.ToSingle(structRot["x"]), Convert.ToSingle(structRot["y"]), Convert.ToSingle(structRot["z"]))) + OriginRotation);
                Vector3 TempPos = OriginRot * (new Vector3(Convert.ToSingle(structPos["x"]), Convert.ToSingle(structPos["y"]), Convert.ToSingle(structPos["z"])));
                Vector3 NewPos = TempPos + targetPoint;
                Item newItem = ItemManager.CreateByName(prefabname,1);
                if (newItem != null)
                {
                    SpawnDeployable(newItem, NewPos, newAngles, player);
                }
                else
                {
                    SendReply(player, prefabname);
                }
            }
        }
    }
}
