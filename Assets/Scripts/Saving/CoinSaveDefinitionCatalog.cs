using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Experiment;

namespace SqueakSqueak.Saving
{
    public readonly struct CoinSaveRenderContract
    {
        public string Revision {get;}
        public Color DirtColor {get;}
        public float DirtySmoothness {get;}
        public float MaximumSmoothness {get;}
        public CoinSaveRenderContract(string revision,Color dirtColor,float dirtySmoothness,float maximumSmoothness)
        {
            if(!CoinSaveValidator.ValidId(revision)||!float.IsFinite(dirtColor.r)||!float.IsFinite(dirtColor.g)||!float.IsFinite(dirtColor.b)||!float.IsFinite(dirtColor.a)||
                !float.IsFinite(dirtySmoothness)||dirtySmoothness<0||dirtySmoothness>1||!float.IsFinite(maximumSmoothness)||maximumSmoothness<dirtySmoothness||maximumSmoothness>1)
                throw new ArgumentException("Invalid actual rendering profile");
            Revision=revision;DirtColor=dirtColor;DirtySmoothness=dirtySmoothness;MaximumSmoothness=maximumSmoothness;
        }
    }
    /// <summary>Unity 정의를 생성 시 값으로 고정합니다. 결과 codec/IO는 이 값 계약만 읽습니다.</summary>
    public sealed class CoinSaveDefinitionCatalog
    {
        public sealed class Contract
        {
            public string CoinId {get;} public string ReactionId {get;} public string LayoutId {get;}
            public string Fingerprint {get;} public string RenderRevision {get;}
            public string SideFingerprint {get;}
            public string SideRenderRevision=>"cleaning-room-side-v2";
            public int Version=>CoinSaveLimits.DefinitionVersion;
            private readonly float initialB;private readonly float[] a;
            internal Contract(string coin,string reaction,string layout,string render,string hash,float b,float[] initialA)
            {
                CoinId=coin;ReactionId=reaction;LayoutId=layout;RenderRevision=render;Fingerprint=hash;initialB=b;a=initialA;
                using var sha=SHA256.Create();SideFingerprint=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hash+"|side-cylinder-v2-NxN16-equal-regions-min95"))).Replace("-","").ToLowerInvariant();
            }
            public CoinSurfaceMaskData InitialMask(int resolution,bool side=false)=>new(resolution,initialB,CoinSurfaceDirtPattern.FixedPatches,0,side);
            public int ACount=>a.Length;
            public float InitialA(int i)=>a[i];
        }
        private readonly Dictionary<string,Contract> entries=new(StringComparer.Ordinal);
        // 셰이더/씬의 표현 의미 변경 시 호출자가 명시적인 렌더 계약을 갱신해야 합니다.
        public CoinSaveDefinitionCatalog(CleaningExperimentDefinition definition,CoinSaveRenderContract render)
        {
            var renderRevision=render.Revision;
            if(definition==null||definition.Validate()!=null||definition.Placements.Count>64||!CoinSaveValidator.ValidId(renderRevision))throw new ArgumentException("Invalid save definitions/render revision.");
            foreach(var coin in definition.Coins)
            {
                using var buffer=new MemoryStream();using var writer=new BinaryWriter(buffer);
                writer.Write(CoinSaveLimits.DefinitionVersion);writer.Write(renderRevision);writer.Write(coin.Id);writer.Write(coin.ReactionProfile.Id);writer.Write(definition.LayoutId);
                writer.Write(render.DirtColor.r);writer.Write(render.DirtColor.g);writer.Write(render.DirtColor.b);writer.Write(render.DirtColor.a);writer.Write(render.DirtySmoothness);writer.Write(render.MaximumSmoothness);
                var color=coin.BaseColor;writer.Write(color.r);writer.Write(color.g);writer.Write(color.b);writer.Write(color.a);
                writer.Write(definition.InitialB);writer.Write((int)CoinSurfaceDirtPattern.FixedPatches);
                var initialA=new float[definition.Placements.Count];writer.Write(initialA.Length);
                for(var i=0;i<initialA.Length;i++){var p=definition.Placements[i];initialA[i]=p.Durability;writer.Write((int)p.Face);writer.Write(p.Uv.x);writer.Write(p.Uv.y);writer.Write(p.WorldRadius);writer.Write(p.Durability);}
                foreach(var tool in new[]{CleaningToolId.ExperimentToothbrush,CleaningToolId.ExperimentCloth,CleaningToolId.Dropper})
                {writer.Write(coin.ReactionProfile.ObjectCoefficient(tool));writer.Write(coin.ReactionProfile.SurfaceCoefficient(tool,false));writer.Write(coin.ReactionProfile.SurfaceCoefficient(tool,true));}
                var mesh=coin.SurfaceMesh;var vertices=mesh.vertices;var normals=mesh.normals;var triangles=mesh.triangles;
                writer.Write(vertices.Length);foreach(var v in vertices){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
                writer.Write(normals.Length);foreach(var n in normals){writer.Write(n.x);writer.Write(n.y);writer.Write(n.z);}
                writer.Write(triangles.Length);foreach(var index in triangles)writer.Write(index);
                var material=coin.SurfaceMaterial;writer.Write(material.shader.name);
                foreach(var property in new[]{"_Metallic","_Smoothness","_DirtySmoothness","_MaximumSmoothness","_DetailContrast","_ReliefStrength"})
                {writer.Write(property);writer.Write(material.HasProperty(property)?material.GetFloat(property):0);}
                var dirt=material.GetColor("_DirtColor");writer.Write(dirt.r);writer.Write(dirt.g);writer.Write(dirt.b);writer.Write(dirt.a);
                writer.Flush();using var sha=SHA256.Create();var hash=BitConverter.ToString(sha.ComputeHash(buffer.ToArray())).Replace("-","").ToLowerInvariant();
                entries.Add(coin.Id,new Contract(coin.Id,coin.ReactionProfile.Id,definition.LayoutId,renderRevision,hash,definition.InitialB,initialA));
            }
        }
        public Contract Get(string coinId)
        {if(coinId==null||!entries.TryGetValue(coinId,out var value))throw new SaveDataException(SaveIssue.UnknownDefinition,"Unknown saved coin: "+coinId);return value;}
    }
}
