using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Archipelago.HollowKnight
{
    public class ShadeInfo
    {
        public string Scene { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public Vector3 MapPos { get; set; }
        public string MapZone { get; set; }
        public int Health { get; set; }
        public int MP { get; set; }
        public int FireballLevel { get; set; }
        public int QuakeLevel { get; set; }
        public int ScreamLevel { get; set; }
        public int SpecialType { get; set; }
        public int Geo { get; set; }

        public ShadeInfo()
        {
        }

        public static ShadeInfo FromPlayerData()
        {
            PlayerData pd = PlayerData.instance;
            if (!pd.soulLimited)
            {
                return null;
            }
            ShadeInfo si = new();
            si.ReadPlayerData();
            return si;
        }

        public void ReadPlayerData()
        {
            PlayerData pd = PlayerData.instance;
            Scene = pd.shadeScene;
            PositionX = pd.shadePositionX;
            PositionY = pd.shadePositionY;
            MapPos = pd.shadeMapPos;
            MapZone = pd.shadeMapZone;
            Health = pd.shadeHealth;
            MP = pd.shadeMP;
            FireballLevel = pd.shadeFireballLevel;
            QuakeLevel = pd.shadeQuakeLevel;
            ScreamLevel = pd.shadeScreamLevel;
            SpecialType = pd.shadeSpecialType;
            Geo = pd.geoPool;
        }

        public void WritePlayerData()
        {
            PlayerData pd = PlayerData.instance;
            pd.shadeScene = Scene;
            pd.shadePositionX = PositionX;
            pd.shadePositionY = PositionY;
            pd.shadeMapPos = MapPos;
            pd.shadeMapZone = MapZone;
            pd.shadeHealth = Health;
            pd.shadeMP = MP;
            pd.shadeFireballLevel = FireballLevel;
            pd.shadeQuakeLevel = QuakeLevel;
            pd.shadeScreamLevel = ScreamLevel;
            pd.shadeSpecialType = SpecialType;
            pd.geoPool = Geo;
        }
    }}
