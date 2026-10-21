using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Experiment;
using SqueakSqueak.Saving;
using SqueakSqueak.Progression;

namespace SqueakSqueak.EditorValidation
{
    public static class CoinSideCleaningValidation
    {
        public static string RunNormal(string directory)
        {
            if(!Application.isPlaying||!EditorApplication.isPaused)throw new InvalidOperationException("Fresh normal Play/Pause required");
            directory=CleaningExperimentController.ValidateIsolatedPath(directory);var count=0;
            void Check(bool value,string name){if(!value)throw new InvalidOperationException("SIDE NORMAL FAIL "+name);count++;}
            var c=UnityEngine.Object.FindFirstObjectByType<CleaningExperimentController>();var stage=UnityEngine.Object.FindFirstObjectByType<CleaningStageController>();
            var surface=UnityEngine.Object.FindFirstObjectByType<CoinSurfaceContamination>();var pointer=UnityEngine.Object.FindFirstObjectByType<CoinSurfacePointer>();
            var completion=UnityEngine.Object.FindFirstObjectByType<CleaningCompletionController>();var selection=UnityEngine.Object.FindFirstObjectByType<CleaningToolSelection>();
            Check(!c.IsActive&&c.SaveDirectory==directory,"isolated normal owner");
            Check(stage.RequestAbandon(stage.RoundId)&&stage.ConfirmAbandon(stage.RoundId),"normal selection state");
            Check(stage.TrySetAccessContext(new StageAccessContext(stage.Catalog.Tools.Select(x=>x.Id),stage.Catalog.Stages.Select(x=>x.Id))),"explicit test-only access fixture (not earned reward)");
            var target=stage.Catalog.Stages.First(x=>x.InitialSurfaceDirt>0&&x.RequiredB>0);
            Check(stage.TryStartStage(target.Id,stage.RoundId)&&surface.SurfaceMask.HasSide,"normal stage generates side contamination");
            var mask=surface.SurfaceMask;var initial=new CoinSurfaceMaskData(mask.Resolution,mask.InitialIntensity,mask.Pattern,mask.InitialPolish,true);
            foreach(var d in UnityEngine.Object.FindObjectsByType<ObjectDirt>(FindObjectsSortMode.None))if(d.transform.parent.name=="StageDirt")d.ApplyCleaning(100);
            foreach(var face in new[]{CoinSurfaceFace.Front,CoinSurfaceFace.Back})for(var i=0;i<mask.TexelCount;i++){mask.TrySetDirt(face,i,0);mask.TrySetPolish(face,i,1);}
            surface.UploadSurfaceMasks();Check(!completion.TryCompleteIfEligible()&&stage.State==CleaningStageState.Cleaning,"normal dirty side prevents completion after caps/A");
            var tool=Enumerable.Range(0,selection.AvailableToolCount).Select(selection.GetToolAt).First(x=>x.SurfaceDirtRemovalPerSecond>0);Check(selection.TrySelect(tool),"normal allowed cleaning tool selected");
            var workMatrix=pointer.SurfaceLocalToWorldMatrix;
            var start=CoinSideSurface.Contact(workMatrix,pointer.SurfaceLocalBounds,.998f,.5f);var end=CoinSideSurface.Contact(workMatrix,pointer.SurfaceLocalBounds,.002f,.5f);
            var before=mask.GetCurrentDirtSum(CoinSurfaceFace.Side);var sample=new CleaningRubSample(start,end,.1f,Time.frameCount+800000);
            Check((bool)typeof(CoinSurfaceContamination).GetMethod("ApplyRubSample",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(surface,new object[]{sample})&&mask.GetCurrentDirtSum(CoinSurfaceFace.Side)<before,"normal owner approved rub removes side B");
            for(var i=0;i<mask.SideTexelCount;i++)mask.TrySetDirt(CoinSurfaceFace.Side,i,initial.GetDirt(CoinSurfaceFace.Side,i)*(1-target.RequiredB+.01f));
            surface.UploadSurfaceMasks();Check(surface.SurfaceDirtRemovalRatio>=target.RequiredB&&surface.SideDirtRemovalRatio<target.RequiredB&&!completion.TryCompleteIfEligible(),"normal side minimum independently blocks average-only completion");
            for(var i=0;i<mask.SideTexelCount;i++){mask.TrySetDirt(CoinSurfaceFace.Side,i,0);mask.TrySetPolish(CoinSurfaceFace.Side,i,1);}surface.UploadSurfaceMasks();
            Check(completion.TryCompleteIfEligible()&&stage.State==CleaningStageState.Result,"normal side/caps criteria allow completion");
            var polish=new CoinSurfaceMaskData(128,0,CoinSurfaceDirtPattern.Uniform,0,true);var brush=new CoinSurfaceBrushStroke();
            Check(brush.ApplyApprovedSweep(polish,start,end,workMatrix,pointer.SurfaceLocalBounds,tool,.2f,0,.1f,1)&&polish.GetCurrentPolishSum(CoinSurfaceFace.Side)>0&&polish.GetCurrentPolishSum(CoinSurfaceFace.Front)==0,"general side polish footprint never polishes caps");
            var report="PASS side normal checks="+count+" stage="+target.Id+" Bgoal="+target.RequiredB+"; isolated access fixture, synthetic accepted rub/completion";
            File.WriteAllText(Path.Combine(directory,"side-normal.txt"),report);return report;
        }
        public static string Run(string directory)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit required");
            directory=CleaningExperimentController.ValidateIsolatedPath(directory);Directory.CreateDirectory(directory);
            var checks=new System.Collections.Generic.List<string>();
            void Check(bool value,string name){if(!value)throw new InvalidOperationException("SIDE FAIL "+name);checks.Add(name);}
            void Reject(Action action,SaveIssue issue,string name){try{action();}catch(SaveDataException e){Check(e.Issue==issue,name);return;}throw new Exception("Expected rejection: "+name);}
            var bounds=new Bounds(Vector3.zero,new Vector3(1,2,1));var tool=new object();
            foreach(var rotation in new[]{Quaternion.identity,Quaternion.Euler(90,0,0),Quaternion.Euler(23,61,17)})
            {
                var matrix=Matrix4x4.TRS(new Vector3(2,3,4),rotation,new Vector3(2,.1f,2));
                var from=CoinSideSurface.Contact(matrix,bounds,.998f,.5f);var to=CoinSideSurface.Contact(matrix,bounds,.002f,.5f);
                Check(CoinSurfaceMapper.TryMapPoint(matrix,bounds,from.WorldPosition,from.WorldNormal,out var mapped)&&mapped.IsCleanable&&mapped.Face==CoinSurfaceFace.Side&&Vector2.Distance(mapped.Uv,from.Uv)<1e-5f,"rotated side mapping "+rotation);
                Check(Mathf.Abs(CoinSideSurface.Distance(matrix,from,to)-2*Mathf.PI*.004f)<1e-5f,"short wrapped circumference distance");
                var bottom=CoinSideSurface.Contact(matrix,bounds,0,0);
                Check(CoinSurfaceMapper.IsWithinBrush(matrix,bottom,CoinSideSurface.Contact(matrix,bounds,0,.149f/.2f),.15f)&&!CoinSurfaceMapper.IsWithinBrush(matrix,bottom,CoinSideSurface.Contact(matrix,bounds,0,.151f/.2f),.15f),"world height footprint boundary");
                Check(CoinSurfaceMapper.IsWithinBrush(matrix,from,CoinSideSurface.Contact(matrix,bounds,from.Uv.x+.149f/(2*Mathf.PI),.5f),.15f)&&!CoinSurfaceMapper.IsWithinBrush(matrix,from,CoinSideSurface.Contact(matrix,bounds,from.Uv.x+.151f/(2*Mathf.PI),.5f),.15f),"world circumference footprint boundary");
                var data=new CoinSurfaceMaskData(128,1,CoinSurfaceDirtPattern.Uniform,0,true);var stroke=new CoinSurfaceBrushStroke();
                Check(stroke.ApplyApprovedSweep(data,from,to,matrix,bounds,tool,.15f,1,.1f),"wrapped approved stroke changes side");
                Check(data.GetDirt(CoinSurfaceFace.Side,4*128)<1&&data.GetDirt(CoinSurfaceFace.Side,4*128+127)<1&&data.GetDirt(CoinSurfaceFace.Side,4*128+64)==1,"both seam edges changed; opposite circumference intact");
                Check(data.GetCurrentDirtSum(CoinSurfaceFace.Front)==data.GetInitialDirtSum(CoinSurfaceFace.Front)&&data.GetCurrentDirtSum(CoinSurfaceFace.Back)==data.GetInitialDirtSum(CoinSurfaceFace.Back),"side stroke cannot paint caps");
                var motion=new CleaningRubMotion();Check(!motion.Sample(0,true,from,matrix,tool,data,.016f).IsRubbing,"initial click no rub");
                for(var i=1;i<=8;i++)Check(!motion.Sample(i,true,from,matrix,tool,data,.016f).IsRubbing,"stationary side no rub "+i);
                var sample=motion.Sample(9,true,to,matrix,tool,data,.016f);Check(sample.IsRubbing&&sample.EffectiveSeconds<=.016001f,"seam movement uses one frame budget");
                Check(!motion.Sample(10,false,to,matrix,tool,data,.016f).IsRubbing&&!motion.Sample(11,true,from,matrix,tool,data,.016f).IsRubbing,"UI/MMB eligibility break cannot bridge");
                CoinSurfaceMapper.TryMapPoint(matrix,bounds,matrix.MultiplyPoint3x4(new Vector3(0,1,0)),matrix.inverse.transpose.MultiplyVector(Vector3.up),out var cap);
                Check(!motion.Sample(12,true,cap,matrix,tool,data,.016f).IsRubbing&&!stroke.ApplyApprovedSweep(data,from,cap,matrix,bounds,tool,.15f,1,.1f),"cap/side transition breaks stroke");
                var first=data.InitialDirtSum;data.Reset();Check(data.CurrentDirtSum==first&&data.SideRemovalRatio==0,"side reset restores fixed baseline");
            }
            var definition=AssetDatabase.LoadAssetAtPath<CleaningExperimentDefinition>(CleaningExperimentTools.DefinitionPath);
            foreach(var coin in definition.Coins)
            {
                var matrix=Matrix4x4.Scale(new Vector3(2,.1f,2));var from=CoinSideSurface.Contact(matrix,bounds,.998f,.5f);var to=CoinSideSurface.Contact(matrix,bounds,.002f,.5f);
                var data=new CoinSurfaceMaskData(128,1,CoinSurfaceDirtPattern.Uniform,0,true);var stroke=new CoinSurfaceBrushStroke();
                var before=data.CurrentDirtSum;
                Check(!stroke.ApplyApprovedSweep(data,from,to,matrix,bounds,tool,.15f,1,.1f,0,coin.ReactionProfile,CleaningToolId.ExperimentToothbrush)&&data.CurrentDirtSum==before,"untreated toothbrush coefficient remains zero "+coin.Id);
                Check(CoinSurfaceBrushStroke.SoftenDisk(data,from,matrix,bounds,.15f)>0,"actual side droplet kernel softens "+coin.Id);
                Check(stroke.ApplyApprovedSweep(data,from,to,matrix,bounds,tool,.15f,1,.1f,0,coin.ReactionProfile,CleaningToolId.ExperimentCloth),"treated cloth side reaction "+coin.Id);
            }
            var definitions=new CoinSaveDefinitionCatalog(definition,CoinSaveValidation.ActualRenderContract());var codec=new CompletedCoinCodec(definitions);
            var source=Path.GetFullPath(".utmp/s307-player-1792551541225");var mixed=Path.Combine(directory,"mixed");Directory.CreateDirectory(mixed);
            foreach(var name in new[]{CompletedCoinStore.FileName,GeneralProgressStore.FileName})File.Copy(Path.Combine(source,name),Path.Combine(mixed,name));
            var original=File.ReadAllBytes(Path.Combine(mixed,CompletedCoinStore.FileName));var legacy=codec.Decode(original).Data;
            Check(legacy.results.Length==2&&legacy.results.All(x=>x.formatVersion==1),"actual old v1 fixture decodes without changed-definition/reset");
            var store=new CompletedCoinStore(mixed,definitions);Check(store.Save(legacy.results[0]).Status==SaveWriteStatus.AlreadySaved&&File.ReadAllBytes(Path.Combine(mixed,CompletedCoinStore.FileName)).SequenceEqual(original),"v1 same ID retry leaves old bytes intact");
            var contract=definitions.Get(legacy.results[0].coinId);var mask=contract.InitialMask(128,true);
            foreach(var face in new[]{CoinSurfaceFace.Front,CoinSurfaceFace.Back})for(var i=0;i<mask.TexelCount;i++)mask.TrySetDirt(face,i,0);
            Reject(()=>SavedCoinDto.FromSnapshot(new ExperimentSnapshot(99,contract.CoinId,contract.ReactionId,contract.LayoutId,mask,new float[contract.ACount],7),definitions),SaveIssue.Corrupt,"dirty side alone cannot create completed save");
            for(var i=0;i<mask.SideTexelCount;i++){mask.TrySetDirt(CoinSurfaceFace.Side,i,mask.GetDirt(CoinSurfaceFace.Side,i)*.03f);if(i%3==0)mask.TrySoften(CoinSurfaceFace.Side,i);}
            var snapshot=new ExperimentSnapshot(100,contract.CoinId,contract.ReactionId,contract.LayoutId,mask,new float[contract.ACount],12.345f);var candidate=SavedCoinDto.FromSnapshot(snapshot,definitions);
            Check(candidate.formatVersion==2&&candidate.sideDirt.Length==mask.SideTexelCount,"new completed result is v2 with side data");
            Check(store.Save(candidate).Succeeded,"v2 appended beside v1 actual safe IO");var loaded=store.Load();
            Check(loaded.HasData&&loaded.Data.results.Length==3&&codec.SameResult(legacy.results[0],loaded.Data.results[0])&&codec.SameResult(legacy.results[1],loaded.Data.results[1])&&codec.SameResult(candidate,loaded.Data.results[2]),"mixed v1/v2 preserves exact old and new result payloads");
            var stage=UnityEngine.Object.FindFirstObjectByType<CleaningStageController>();var session=new CoinSaveSession(mixed,definitions,new GeneralProgressSaveDefinition(stage.Catalog));var values=session.Load();
            Check(values.Succeeded&&!values.Results[0].HasSide&&!session.RestoreMask(values.Results[0]).HasSide&&values.Results[2].HasSide,"mixed reload marks legacy no-side instead of new contamination");
            var restored=session.RestoreMask(values.Results[2]);Check(Enumerable.Range(0,restored.SideTexelCount).All(i=>restored.GetDirt(CoinSurfaceFace.Side,i)==snapshot.SideDirt[i]&&restored.IsSoftened(CoinSurfaceFace.Side,i)==snapshot.SideSoftened[i]),"v2 exact side floats/softening restore");
            var after=File.ReadAllBytes(Path.Combine(mixed,CompletedCoinStore.FileName));Check(store.Save(SavedCoinDto.FromSnapshot(values.Results[0],definitions)).Status==SaveWriteStatus.AlreadySaved&&File.ReadAllBytes(Path.Combine(mixed,CompletedCoinStore.FileName)).SequenceEqual(after),"restored v1 same-ID retry does not convert to v2 or rewrite mixed file");
            var bad=candidate.Copy();bad.sideDirt[0]=float.NaN;Reject(()=>new CoinSaveValidator(definitions).ValidateResult(bad),SaveIssue.Corrupt,"side NaN rejected");
            bad=candidate.Copy();bad.formatVersion=3;Reject(()=>new CoinSaveValidator(definitions).ValidateResult(bad),SaveIssue.Unsupported,"future result rejected");
            Reject(()=>codec.Decode(after.Take(after.Length-1).ToArray()),SaveIssue.Corrupt,"truncated mixed payload rejected");
            Check(File.ReadAllBytes(Path.Combine(source,CompletedCoinStore.FileName)).SequenceEqual(original),"source fixture never changed");
            var report="PASS side CPU/compat checks="+checks.Count;File.WriteAllLines(Path.Combine(directory,"side-cpu.txt"),new[]{report}.Concat(checks));return report;
        }
    }
}
