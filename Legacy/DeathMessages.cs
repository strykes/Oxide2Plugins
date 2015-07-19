// Reference: Oxide.Ext.RustLegacy
// Reference: Facepunch.ID
// Reference: Facepunch.MeshBatch
// Reference: Facepunch.HitBox
// Reference: Google.ProtocolBuffers

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using RustProto;

namespace Oxide.Plugins
{
    [Info("DeathMessages", "Reneb", "1.0.0")]
    class DeathMessages : RustLegacyPlugin
    {

        /////////////////////////
        // Config Management
        /////////////////////////
        public static bool suicide = true;
        public static bool player = true;
        public static bool animals = true;

        void LoadDefaultConfig() { }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        void Init()
        {
            CheckCfg<bool>("Show: suicide", ref suicide);
            CheckCfg<bool>("Show: player pvp kills", ref player);
            CheckCfg<bool>("Show: player pve deaths", ref animals);

            SaveConfig();
        }
        void Loaded()
        {
        } 
        void Unload()
        {

        } 
        void OnKilled(TakeDamage takedamage, DamageEvent damage)
        {
            var tags = new Hash<string, string>();
            if(damage.attacker.client != null)
            {
                tags["weapon"] = (damage.extraData is WeaponImpact) ? ((WeaponImpact)damage.extraData).dataBlock.name : "Unknown";
                tags["killer"] = damage.attacker.client.userName;
                tags["killer-id"] = damage.attacker.client.userID.ToString();
                if (damage.victim.client != null)
                {
                    tags["killed"] = damage.victim.client.userName;
                    tags["killed-id"] = damage.victim.client.userID.ToString();
                    if (damage.victim.client == damage.attacker.client)
                    {
                        if (suicide)
                        {
                            notifyDeath(tags, "suicideDeathMessage");
                        }
                        return;
                    }
                    else
                    {
                        if (player)
                        {
                            tags["distance"] = Math.Floor(Vector3.Distance(damage.victim.client.lastKnownPosition, damage.attacker.client.lastKnownPosition)).ToString();
                            tags["bodypart"] = BodyParts.GetNiceName(damage.bodyPart);
                            notifyDeath(tags, "playerDeathMessage");
                        }
                    }
                }
                else if(damage.victim.networkView != null && damage.victim.networkView.gameObject.GetComponent<HostileWildlifeAI>())
                {
                    HostileWildlifeAI hostileai = damage.victim.networkView.gameObject.GetComponent<HostileWildlifeAI>();
                    tags["distance"] = Math.Floor(Vector3.Distance(damage.victim.networkView.position, damage.attacker.client.lastKnownPosition)).ToString();
                    tags["killed"] = hostileai.gameObject.name.Replace("(Clone)", "").Replace("Mutant", "Mutant ").ToLower();
                    tags["killed-id"] = tags["killed"].Replace("mutant ", "");

                }
            }
            else if (damage.attacker.networkView != null && damage.attacker.networkView.gameObject.GetComponent<HostileWildlifeAI>())
            {
                HostileWildlifeAI hostileai = damage.attacker.networkView.gameObject.GetComponent<HostileWildlifeAI>();
                tags["killer"] = hostileai.gameObject.name.Replace("(Clone)","").Replace("Mutant", "Mutant ").ToLower();
                tags["killer-id"] = tags["killer"].Replace("mutant ", "");
                if(animals)
                    notifyDeath(tags, "deathByAnimal");
            }
        }
        void notifyDeath(Hash<string,string> tagData, string messagename)
        {

        }
    }
}

