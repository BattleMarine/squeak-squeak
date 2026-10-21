#if UNITY_EDITOR || SQUEAK_SAVE_VALIDATION
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Experiment;

namespace SqueakSqueak.Saving
{
    public sealed partial class SavePlayerProbe
    {
        private IEnumerator RunSideCleaning()
        {
            while(!c.SaveReady){if(c.LastError!=null)throw new Exception(c.LastError);yield return null;}while(c.PersistenceBusy)yield return null;
            var identity=Path.Combine(directory,"side-identities.txt");var generalPath=Path.Combine(directory,GeneralProgressStore.FileName);var general=File.ReadAllBytes(generalPath);
            var session=(CoinSaveSession)IntegrationField("saveSession");var disk=new CompletedCoinStore(directory,session.Definitions);var events=0;c.ResultStored+=_=>events++;
            coinTransform=(Transform)IntegrationField("coinRoot");
            if(phase=="side-reload")
            {
                Check(c.StoredResults.Count==4&&c.StoredResults.Count(x=>x.HasSide)==2&&!c.IsActive&&!tracker.HasStarted&&c.PendingResultId==null,"mixed restart restores two legacy/two side results, not unfinished work");
                Check(File.ReadAllLines(identity).SequenceEqual(c.StoredResults.Select(x=>x.ResultId+" "+Digest(x))),"all legacy/new IDs/time/three-region arrays exact across restart");
                foreach(var value in c.StoredResults.ToArray())
                {
                    Check(c.ViewStoredResult(value.ResultId),"reload opens legacy/new result "+value.CoinId+" side="+value.HasSide);CheckMasks(value);yield return null;
                    if(value.HasSide){Check(SideGpuDigest()==File.ReadAllText(Path.Combine(directory,"gpu-"+value.ResultId+".txt")),"side RGBAHalf bytes exact after process restart");CaptureSide("reload-"+value.CoinId);}
                    else Check(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Any(x=>x.name=="RB.Info"&&x.text.Contains("옆면 없음")),"legacy observation explicitly identifies absent side data");
                    var revision=surface.SurfaceMask.Revision;Check(!surface.ApplyCleaning(1)&&!surface.ApplyDroplet(default,.2f,Time.frameCount)&&!c.SelectTool(1),"readonly blocks side work");
                    yield return null;Check(surface.SurfaceMask.Revision==revision&&events==0,"readonly has no new data/event");
                }
                Check(File.ReadAllBytes(generalPath).SequenceEqual(general),"reload keeps general progress bytes");Finish("PASS side-reload checks="+checks.Count+"; mixed version restore/side GPU; not physical input");yield break;
            }
            Check(c.StoredResults.Count==2&&c.StoredResults.All(x=>!x.HasSide),"actual copied v1 product fixture loaded");
            var legacy=c.StoredResults.Select(x=>x.ResultId+" "+Digest(x)).ToArray();var original=File.ReadAllBytes(Path.Combine(directory,CompletedCoinStore.FileName));
            Check(disk.Save(SavedCoinDto.FromSnapshot(c.StoredResults[0],session.Definitions)).Status==SaveWriteStatus.AlreadySaved&&File.ReadAllBytes(Path.Combine(directory,CompletedCoinStore.FileName)).SequenceEqual(original),"legacy same-ID product snapshot retry does not rewrite");
            Check(c.ViewStoredResult(c.StoredResults[0].ResultId)&&!surface.SurfaceMask.HasSide&&surface.SideDirtMask==null,"legacy restore attaches no invented side mask");
            CaptureSide("legacy-no-side");UiCapture("legacy-label");
            for(var n=0;n<2;n++)
            {
                Check(c.RequestCoin(n)&&surface.SurfaceMask.HasSide&&surface.SideDirtMask!=null,"fresh M"+(n+1)+" creates side contamination");yield return null;
                var hit=CaptureSide("dirty-M"+(n+1));Check(hit.IsCleanable&&hit.Face==CoinSurfaceFace.Side,"actual rotated MeshCollider side hit");
                Check(c.ToggleScanner(),"scanner enables on dirty side");CaptureSide("scanner-M"+(n+1));Check(c.ToggleScanner(),"scanner off");
                foreach(var dirt in c.Dirt)dirt.ApplyCleaning(100);
                foreach(var face in new[]{CoinSurfaceFace.Front,CoinSurfaceFace.Back})for(var i=0;i<surface.SurfaceMask.TexelCount;i++)surface.SurfaceMask.TrySetDirt(face,i,0);
                surface.UploadSurfaceMasks();c.EvaluateGoal();Check(c.State==ExperimentState.Cleaning&&surface.SurfaceMask.SideRemovalRatio==0&&c.Progress.DisplayPercent<100,"caps/A clean but dirty side cannot complete");
                SideWork(hit);PulseFinalizationClock(1);Complete(0);var goal=FinalizationSample();Check(c.State==ExperimentState.Ready,"three-region goal reached");
                yield return null;
                var data=surface.SurfaceMask;var index=Enumerable.Range(0,data.SideTexelCount).First(i=>data.GetDirt(CoinSurfaceFace.Side,i)>0&&!data.IsSoftened(CoinSurfaceFace.Side,i));
                SideWork(CoinSideSurface.Contact(pointer.SurfaceLocalToWorldMatrix,pointer.SurfaceLocalBounds,(index%data.Resolution+.5f)/data.Resolution,(index/data.Resolution+.5f)/data.SideHeight));PulseFinalizationClock(1);
                Check(!goal.SideDirt.SequenceEqual(FinalizationSample().SideDirt)&&tracker.ElapsedSeconds>goal.ElapsedSeconds,"postgoal side work remains possible before final confirmation");
                Click("보관 확인");var confirmed=FinalizationSample();Click("확인");while(c.PersistenceBusy)yield return null;
                Check(c.State==ExperimentState.Stored&&events==n+1&&c.CurrentResult.HasSide&&SameFinalizationArrays(confirmed,c.CurrentResult),"v2 final three-region state stored once");
                CheckMasks(c.CurrentResult);File.WriteAllText(Path.Combine(directory,"gpu-"+c.CurrentResult.ResultId+".txt"),SideGpuDigest());
                CaptureSide("stored-M"+(n+1));
            }
            var newest=c.CurrentResult.ResultId;
            for(var n=0;n<3;n++)
            {
                Check(c.ViewStoredResult(c.StoredResults[0].ResultId),"legacy texture switch "+n);yield return null;yield return null;
                Check(Resources.FindObjectsOfTypeAll<Texture2D>().Count(x=>x.name.StartsWith("SqueakSqueak.BMask.",StringComparison.Ordinal))==2,"legacy owns two mask textures "+n);
                Check(c.ViewStoredResult(newest),"new texture switch "+n);yield return null;yield return null;
                Check(Resources.FindObjectsOfTypeAll<Texture2D>().Count(x=>x.name.StartsWith("SqueakSqueak.BMask.",StringComparison.Ordinal))==3,"side result owns three mask textures without accumulation "+n);
            }
            Check(legacy.SequenceEqual(c.StoredResults.Take(2).Select(x=>x.ResultId+" "+Digest(x)))&&events==2&&disk.Load().Data.results.Length==4,"legacy identity/state unchanged, exactly two new unique results");
            Check(File.ReadAllBytes(generalPath).SequenceEqual(general),"side work/storage never grants ownership/completion");
            File.WriteAllLines(identity,c.StoredResults.Select(x=>x.ResultId+" "+Digest(x)));
            Check(c.RequestCoin(0),"unfinished side round before quit");yield return null;var unfinished=CaptureSide("unfinished");SideWork(unfinished);
            Check(c.RequestQuit()&&c.State==ExperimentState.QuitConfirmation,"unfinished side work receives quit warning");
            Finish("PASS side-seed checks="+checks.Count+"; actual mesh/GPU/IO/UI, synthetic approved rub/completion/clock; product confirm quit follows",false);Click("확인");
        }
        private void SideWork(CoinSurfaceContact start)
        {
            var before=surface.SurfaceMask.GetCurrentDirtSum(CoinSurfaceFace.Side);var caps=surface.SurfaceMask.GetCurrentDirtSum(CoinSurfaceFace.Front)+surface.SurfaceMask.GetCurrentDirtSum(CoinSurfaceFace.Back);
            Check(c.SelectTool(2)&&surface.ApplyDroplet(start,.2f,Time.frameCount),"product drop applies to side");Check(c.SelectTool(1),"side cloth selected");
            var end=CoinSideSurface.Contact(pointer.SurfaceLocalToWorldMatrix,pointer.SurfaceLocalBounds,start.Uv.x+.008f,start.Uv.y);
            var sample=new CleaningRubSample(start,end,.1f,Time.frameCount+900000);
            Check((bool)typeof(CoinSurfaceContamination).GetMethod("ApplyRubSample",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(surface,new object[]{sample}),"approved side cloth changes surface");
            Check(surface.SurfaceMask.GetCurrentDirtSum(CoinSurfaceFace.Side)<before&&caps==surface.SurfaceMask.GetCurrentDirtSum(CoinSurfaceFace.Front)+surface.SurfaceMask.GetCurrentDirtSum(CoinSurfaceFace.Back),"side work removes only side B");
        }
        private string SideGpuDigest(){using var hash=SHA256.Create();return Convert.ToBase64String(hash.ComputeHash(surface.SideDirtMask.GetRawTextureData()));}
        private CoinSurfaceContact CaptureSide(string name)
        {
            var camera=pointer.GameplayCamera;pointer.ResetViewToInitialState();coinTransform.rotation=Quaternion.AngleAxis(90,camera.transform.right)*coinTransform.rotation;Physics.SyncTransforms();
            var collider=(MeshCollider)IntegrationField("coinCollider");var screen=camera.WorldToScreenPoint(coinTransform.position);
            Check(collider.Raycast(camera.ScreenPointToRay(screen),out var hit,100)&&CoinSurfaceMapper.TryMapHit(collider,collider.sharedMesh,pointer.SurfaceLocalBounds,hit,out _),"actual rotated mesh raycast "+name);
            CoinSurfaceMapper.TryMapHit(collider,collider.sharedMesh,pointer.SurfaceLocalBounds,hit,out var contact);Check(contact.Face==CoinSurfaceFace.Side,"side classified "+name);
            var renderer=(Renderer)IntegrationField("coinRenderer");var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
            var target=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);var image=new Texture2D(1280,720,TextureFormat.RGB24,false,true);
            var prior=block.GetFloat("_SurfaceDebug");
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());
                block.SetFloat("_SurfaceDebug",1);renderer.SetPropertyBlock(block);camera.Render();image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,name+"-mask.png"),image.EncodeToPNG());
                var viewport=camera.WorldToViewportPoint(hit.point);var pixel=image.GetPixel(Mathf.Clamp((int)(viewport.x*1280),0,1279),Mathf.Clamp((int)(viewport.y*720),0,719));
                var expected=surface.SideDirtMask==null?0:surface.SideDirtMask.GetPixel(Mathf.Clamp((int)(contact.Uv.x*surface.SideDirtMask.width),0,surface.SideDirtMask.width-1),Mathf.Clamp((int)(contact.Uv.y*surface.SideDirtMask.height),0,surface.SideDirtMask.height-1)).r;
                Check(Mathf.Abs(pixel.r-expected)<.02f&&Mathf.Abs(pixel.b-.5f)<.02f,"shader side sample matches side texture/legacy zero "+name+" r="+pixel.r+" expected="+expected);
            }
            finally{block.SetFloat("_SurfaceDebug",prior);renderer.SetPropertyBlock(block);camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Destroy(image);}
            return contact;
        }
    }
}
#endif
