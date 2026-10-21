#if UNITY_EDITOR || SQUEAK_SAVE_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Experiment;
using SqueakSqueak.Progression;

namespace SqueakSqueak.Saving
{
    /// <summary>검증 define 전용 probe. 정식 Player에는 클래스/경로 override/합성 청소가 포함되지 않습니다.</summary>
    public sealed partial class SavePlayerProbe:MonoBehaviour
    {
        public static string IsolatedDirectory {get;private set;}
        public static string LastReport {get;private set;}
        private string directory,phase;
        private readonly List<string> checks=new();
        private float deadline;
        private bool finished;
        private CleaningExperimentController c;
        private CoinSurfaceContamination surface;
        private CoinSurfacePointer pointer;
        private CleaningStageController stage;
        private CleaningProgressTracker tracker;
        private Transform coinTransform;
        private bool editorRun;
        private bool renderMismatch;
        #if SQUEAK_SAVE_VALIDATION && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigurePlayer()
        {
            var args=Environment.GetCommandLineArgs();
            var index=Array.IndexOf(args,"-saveProbeDirectory");
            IsolatedDirectory=CleaningExperimentController.ValidateIsolatedPath(index>=0&&index+1<args.Length?args[index+1]:throw new ArgumentException("Missing isolated validation directory"));
            var p=Array.IndexOf(args,"-saveProbePhase");var phase=p>=0&&p+1<args.Length?args[p+1]:"seed";
            var go=new GameObject("Save.ValidationProbe");DontDestroyOnLoad(go);var probe=go.AddComponent<SavePlayerProbe>();probe.directory=IsolatedDirectory;probe.phase=phase;
            Application.runInBackground=true;
        }
        #endif
        public static void BeginEditor(string path,string mode)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play required");
            var go=new GameObject("Save.ValidationProbe");var probe=go.AddComponent<SavePlayerProbe>();probe.directory=CleaningExperimentController.ValidateIsolatedPath(path);probe.phase=mode;probe.editorRun=true;
        }
        private void Start(){deadline=Time.realtimeSinceStartup+120;StartCoroutine(Guard(Run()));}
        private void Update(){if(!finished&&Time.realtimeSinceStartup>deadline)Finish("FAIL timeout: "+(c==null?"controller missing":c.State+" / "+c.LastError));}
        private IEnumerator Guard(IEnumerator work)
        {
            while(!finished)
            {
                object current;
                try{if(!work.MoveNext())break;current=work.Current;}
                catch(Exception e){Finish("FAIL "+e);yield break;}
                yield return current;
            }
        }
        private void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);checks.Add(name);}
        private IEnumerator Run()
        {
            yield return null;
            c=FindFirstObjectByType<CleaningExperimentController>();surface=FindFirstObjectByType<CoinSurfaceContamination>();pointer=FindFirstObjectByType<CoinSurfacePointer>();
            tracker=FindFirstObjectByType<CleaningProgressTracker>();stage=FindFirstObjectByType<CleaningStageController>();
            Check(c!=null&&c.SaveDirectory==directory,"isolated product path");
            if(phase=="final-seed"||phase=="final-reload")
            {
                var work=RunFinalRegression();while(work.MoveNext())yield return work.Current;yield break;
            }
            if(phase=="ui-flow"||phase=="ui-capacity")
            {
                var work=RunUiFlow();while(work.MoveNext())yield return work.Current;yield break;
            }
            if(phase=="integration-seed"||phase=="integration-reload")
            {
                var integration=RunIntegration();
                while(integration.MoveNext())yield return integration.Current;
                yield break;
            }
            if(phase=="finalization-seed"||phase=="finalization-reload")
            {
                var work=RunFinalization();
                while(work.MoveNext())yield return work.Current;
                yield break;
            }
            if(phase=="blocked")
            {
                while(c.PersistenceBusy)yield return null;
                Check(!c.SaveReady&&c.LastError!=null&&!pointer.enabled&&!stage.enabled,"invalid save blocks startup without reset");
                var path=Path.Combine(directory,CompletedCoinStore.FileName);var bytes=File.ReadAllBytes(path);
                Check(c.RequestQuit(),"load failure -> quit warning");Click("돌아가기");
                Check(ButtonText("재시도")&&!c.SaveReady,"quit cancel restores load retry dialog");
                Check(c.RetryLoad(),"blocked load retry allowed");while(c.PersistenceBusy)yield return null;
                Check(!c.SaveReady&&File.ReadAllBytes(path).SequenceEqual(bytes),"retry preserves unsupported/corrupt original");
                File.Move(path,path+".probe-preserved");if(File.Exists(path+".bak"))File.Move(path+".bak",path+".bak.probe-preserved");
                Check(c.RetryLoad(),"retry after explicit fixture repair");
                while(c.PersistenceBusy)yield return null;
                Check(c.SaveReady&&pointer.enabled&&stage.enabled,"repaired load restores normal work");
                Finish("PASS blocked checks="+checks.Count);yield break;
            }
            while(!c.SaveReady){Check(c.LastError==null,"startup: "+c.LastError);yield return null;}
            Check(stage.enabled&&pointer.enabled&&FindFirstObjectByType<CleaningAudioFeedback>().enabled&&FindFirstObjectByType<CleaningVisualFeedback>().enabled,"startup normal input/audio/visual restored");
            while(c.PersistenceBusy)yield return null;
            Check(c.RequestLibrary(),"fresh normal -> library");
            // 목록 닫기는 실제 버튼 이벤트로 호출하여 원래 입력/효과 복원을 검증합니다.
            FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.name=="RB.Button.목록 닫기").onClick.Invoke();
            Check(stage.enabled&&pointer.enabled&&FindFirstObjectByType<CleaningAudioFeedback>().enabled&&FindFirstObjectByType<CleaningVisualFeedback>().enabled,"library cancel normal feedback restored");
            if(phase=="scene")
            {
                Check(c.RequestCoin(0),"scene case starts coin");Complete(0);c.UseValidationFiles(new DelayedWrite());
                var eventCount=0;c.ResultStored+=_=>eventCount++;
                Check(c.RequestStore(c.RoundId)&&c.ConfirmDialog(c.RoundId),"scene case writes product candidate");
                var id=c.PendingResultId;var old=c;DontDestroyOnLoad(gameObject);
                UnityEngine.SceneManagement.SceneManager.LoadScene("CleaningRoom");yield return null;
                c=FindFirstObjectByType<CleaningExperimentController>();
                while(!c.SaveReady){if(c.LastError!=null)throw new InvalidOperationException("new scene load "+c.LastError);yield return null;}
                Check(old==null&&c.StoredResults.Count==1&&c.StoredResults[0].ResultId==id&&eventCount==0,"destroyed owner not committed; next scene reads completed worker result");
                Check(c.ViewStoredResult(id)&&c.State==ExperimentState.Viewing,"new scene views committed old result");
                surface=FindFirstObjectByType<CoinSurfaceContamination>();CheckMasks(c.CurrentResult);
                Finish("PASS scene checks="+checks.Count);yield break;
            }
            if(phase=="capture"||phase=="gpu")
            {
                Check(c.StoredResults.Count==2,"two committed product results available for isolated GPU checks");
                var lines=File.ReadAllLines(Path.Combine(directory,"seed-identities.txt"));
                for(var n=0;n<2;n++)
                {
                    var value=c.StoredResults[n];Check(lines[n]==value.ResultId+" "+Digest(value),"exact persisted metadata/arrays "+n);
                    Check(c.ViewStoredResult(value.ResultId),"view restored product result "+n);coinTransform=c.Dirt[0].transform.parent.parent;
                    CheckMasks(value);Capture(n,phase);
                    Check(File.ReadAllText(Path.Combine(directory,$"state-seed-{n}-0.txt"))==File.ReadAllText(Path.Combine(directory,$"state-{phase}-{n}-0.txt")),"same material/camera/presentation parameters "+n);
                }
                Finish("PASS "+phase+" checks="+checks.Count+"; independent GPU texture/coin-only mask checks. Full-lit pixel diagnostic mismatch="+renderMismatch);yield break;
            }
            if(phase=="general")
            {
                foreach(var d in FindObjectsByType<ObjectDirt>(FindObjectsSortMode.None).Where(d=>d.transform.parent.name=="StageDirt"))d.ApplyCleaning(100);
                foreach(var face in new[]{CoinSurfaceFace.Front,CoinSurfaceFace.Back})for(var i=0;i<surface.SurfaceMask.TexelCount;i++)surface.SurfaceMask.TrySetDirt(face,i,0);
                surface.UploadSurfaceMasks();Check(FindFirstObjectByType<CleaningCompletionController>().TryCompleteRound(stage.StageId,stage.RoundId),"normal round complete without automatic rewards");
                yield return null;while(c.PersistenceBusy)yield return null;
                var failure=new DeniedWrite();c.UseValidationFiles(failure);
                var completed=new[]{stage.StageId};var tools=stage.Catalog.Tools.Where(t=>string.IsNullOrEmpty(t.UnlockedByStageId)||completed.Contains(t.UnlockedByStageId)).Select(t=>t.Id);
                Check(stage.TrySetAccessContext(new StageAccessContext(tools,completed)),"existing owner receives valid access fixture (not new reward logic)");
                yield return null;while(c.PersistenceBusy)yield return null;
                Check(!c.SaveReady&&!stage.enabled&&!pointer.enabled,"general write failure blocks input");
                Check(c.RequestQuit(),"general failure -> quit confirmation");Click("돌아가기");
                Check(ButtonText("재시도"),"general quit cancel restores retry action");
                failure.Deny=false;Click("재시도");while(c.PersistenceBusy)yield return null;
                Check(c.SaveReady&&stage.enabled&&FindFirstObjectByType<CleaningAudioFeedback>().enabled&&FindFirstObjectByType<CleaningVisualFeedback>().enabled,"general retry success restores normal feedback and dialog");
                var saved=new GeneralProgressStore(directory,new GeneralProgressSaveDefinition(stage.Catalog)).Load();
                Check(saved.HasData&&saved.Data.completedStageIds.SequenceEqual(completed),"only supplied owner access written");
                Finish("PASS general checks="+checks.Count);yield break;
            }
            if(phase=="capacity")
            {
                Check(c.StoredResults.Count==16,"capacity fixture loaded all 16");Check(c.RequestLibrary(),"full library opens");
                Check(FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Count(b=>b.name=="RB.Button.")==16,"16 visible result entries");
                var title=FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Single(t=>t.name=="RB.Library.Title");
                Check(title.rectTransform.anchorMin==new Vector2(.5f,1),"library title top anchored");
                Click("목록 닫기");Check(c.RequestCoin(0),"new coin at capacity still allowed");Complete(0);
                Check(c.RequestStore(c.RoundId)&&!c.ConfirmDialog(c.RoundId)&&c.StoredResults.Count==16&&c.PendingResultId==null,"full library rejects new save without dropping result");
                Check(c.CancelDialog(c.RoundId)&&c.State==ExperimentState.Ready,"capacity cancel resumes same work");
                Finish("PASS capacity checks="+checks.Count);yield break;
            }
            if(phase=="reload")
            {
                Check(c.StoredResults.Count==2,"two stored results loaded across process/domain");
                var lines=File.ReadAllLines(Path.Combine(directory,"seed-identities.txt"));
                for(var n=0;n<2;n++)
                {
                    var snapshot=c.StoredResults[n];Check(lines[n]==snapshot.ResultId+" "+Digest(snapshot),"exact metadata/arrays/ID restored "+n);
                    if(!c.IsActive)Check(c.RequestLibrary(),"normal library before viewing");
                    Check(c.ViewStoredResult(snapshot.ResultId)&&c.State==ExperimentState.Viewing,"normal/stored -> readonly view "+n);
                    coinTransform=c.Dirt[0].transform.parent.parent;
                    Check(surface.SurfaceMask.Resolution==snapshot.Resolution&&surface.FrontDirtMask.width==snapshot.Resolution,"saved mask resolution restored");
                    CheckMasks(snapshot);Check(FindFirstObjectByType<CleaningAudioFeedback>().enabled==false,"view audio blocked");
                    var before=surface.SurfaceMask.Revision;var elapsed=tracker.ElapsedSeconds;
                    Check(!c.RequestStore(c.RoundId)&&!c.SelectTool(2)&&!surface.ApplyCleaning(1),"view clean/tool/store blocked");
                    Check(!surface.ApplyDroplet(default,.2f,Time.frameCount),"view dropper blocked");
                    pointer.ResetViewToInitialState();yield return null;yield return null;
                    Check(pointer.enabled&&surface.SurfaceMask.Revision==before&&tracker.ElapsedSeconds==elapsed,"view frame/F no state or timer mutation");
                    Capture(n,"reload");
                    Check(c.RequestCoin(1-n)&&c.State==ExperimentState.Cleaning,"view -> new coin");
                    Check(FindFirstObjectByType<CleaningAudioFeedback>().enabled&&FindFirstObjectByType<CleaningVisualFeedback>().enabled,"view -> new coin feedback restored");
                    Check(!c.RequestLibrary()&&!c.ViewStoredResult(snapshot.ResultId),"unkept experiment library/view blocked");
                    Check(c.RequestLeave()&&c.ConfirmDialog(c.RoundId),"explicit discard before next library");
                    while(c.PersistenceBusy)yield return null;
                }
                var general=new GeneralProgressStore(directory,new GeneralProgressSaveDefinition(stage.Catalog)).Load();
                Check(general.HasData&&general.Data.ownedToolIds.SequenceEqual(stage.Catalog.InitiallyOwnedToolIds)&&general.Data.completedStageIds.Length==0,"general ownership unchanged; no rewards/unlocks");
            }
            else
            {
                Check(c.StoredResults.Count==0,"fresh completed library empty");
                var identities=new List<string>();var events=0;c.ResultStored+=_=>events++;
                for(var n=0;n<2;n++)
                {
                    Check(c.RequestCoin(n),"begin experiment "+n);coinTransform=c.Dirt[0].transform.parent.parent;
                    Check(FindFirstObjectByType<CleaningAudioFeedback>().enabled&&FindFirstObjectByType<CleaningVisualFeedback>().enabled,"first/new experiment original feedback enabled");
                    Check(!c.RequestLibrary(),"unkept round library denied");
                    Complete(n);
                    Check(c.State==ExperimentState.Ready,"completion allows extra cleaning");
                    var round=c.RoundId;Check(c.RequestStore(round)&&c.CancelDialog(round)&&!c.ConfirmDialog(round-1),"confirm cancel/stale dialog");
                    if(n==0)
                    {
                        var lockPath=Path.Combine(directory,CompletedCoinStore.FileName+".lock");
                        using(var locked=new FileStream(lockPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                        {
                            Check(c.RequestStore(round)&&c.ConfirmDialog(round),"failed candidate starts saving");
                            while(c.PersistenceBusy)yield return null;
                            Check(c.State==ExperimentState.SaveFailed&&!pointer.enabled&&tracker.IsTimingPaused&&c.StoredResults.Count==0,"safe failure preserves round/timer; not stored");
                        }
                        var cancelledId=c.PendingResultId;Check(c.CancelFailedStore()&&c.PendingResultId==null,"safe precommit failure cancel resumes work");
                        surface.SurfaceMask.TrySetDirt(CoinSurfaceFace.Front,Enumerable.Range(0,surface.SurfaceMask.TexelCount).First(i=>surface.SurfaceMask.GetDirt(CoinSurfaceFace.Front,i)>0),0);
                        surface.UploadSurfaceMasks(); // 합성 추가 청소도 실제 제품 경로처럼 GPU를 동기화합니다.
                        var fault=new PostCommitFault();c.UseValidationFiles(fault);
                        Check(c.RequestStore(round)&&c.ConfirmDialog(round)&&c.PendingResultId!=cancelledId,"additional cleaning/new confirmation gets new ID");
                        var pending=c.PendingResultId;Check(!c.ConfirmDialog(round)&&!c.RequestCoin(1),"saving double/stale/new round rejected");
                        c.enabled=false;yield return null;yield return null;
                        Check(c.StoredResults.Count==0,"disabled owner ignores late completion");c.enabled=true;
                        while(c.PersistenceBusy)yield return null;
                        Check(c.State==ExperimentState.SaveFailed&&c.PendingResultId==pending&&!c.CancelFailedStore(),"postcommit uncertainty retains candidate and blocks cancel");
                        Check(c.RequestQuit()&&c.CancelDialog(round)&&ButtonText("같은 결과 재시도"),"uncertain -> quit cancel restores same-ID retry dialog");
                        Check(c.RetryStore(),"retry exact uncertain candidate");
                        while(c.PersistenceBusy)yield return null;
                        Check(c.State==ExperimentState.Stored&&c.CurrentResult.ResultId==pending,"same ID reconciliation after actual postcommit exception");
                        c.UseValidationFiles(null);
                    }
                    else
                    {
                        c.UseValidationFiles(new DelayedWrite());
                        Check(c.RequestStore(round)&&c.ConfirmDialog(round),"second store starts");
                        Check(c.RequestQuit()&&c.State==ExperimentState.Saving,"normal quit request waits for pending worker");
                        var frame=Time.frameCount;
                        while(c.PersistenceBusy)yield return null;
                        Check(Time.frameCount>frame+1,"worker IO keeps actual main frames advancing");
                        Check(c.State==ExperimentState.QuitConfirmation&&c.CancelDialog(round)&&c.State==ExperimentState.Stored,"quit warning after write closes; cancel restores stored state");
                    }
                    Check(c.StoredResults.Count==n+1&&events==n+1,"memory/event committed once "+n);
                    var result=c.CurrentResult;identities.Add(result.ResultId+" "+Digest(result));CheckMasks(result);Capture(n,"seed");
                    Check(!c.ConfirmDialog(round),"stored duplicate dialog rejected");
                }
                File.WriteAllLines(Path.Combine(directory,"seed-identities.txt"),identities);
            }
            Check(!renderMismatch,"all restart render pixels identical");
            Finish("PASS "+phase+" checks="+checks.Count+" (synthetic kernel/product API + actual frames; not physical mouse playtest)");
        }
        private void Complete(int salt)
        {
            foreach(var d in c.Dirt)d.ApplyCleaning(100);
            var mask=surface.SurfaceMask;
            foreach(var face in new[]{CoinSurfaceFace.Front,CoinSurfaceFace.Back})for(var i=0;i<mask.TexelCount;i++)
            {
                mask.TrySetDirt(face,i,mask.GetDirt(face,i)*(i%23<3?.12f+salt*.03f:0));
                if(i%3==0)mask.TrySoften(face,i);
            }
            surface.UploadSurfaceMasks();c.EvaluateGoal();
        }
        private void CheckMasks(ExperimentSnapshot value)
        {
            var mask=surface.SurfaceMask;
            for(var i=0;i<mask.TexelCount;i++)if(mask.GetDirt(CoinSurfaceFace.Front,i)!=value.FrontDirt[i]||mask.GetDirt(CoinSurfaceFace.Back,i)!=value.BackDirt[i]||
                mask.GetPolish(CoinSurfaceFace.Front,i)!=value.FrontPolish[i]||mask.GetPolish(CoinSurfaceFace.Back,i)!=value.BackPolish[i]||
                mask.IsSoftened(CoinSurfaceFace.Front,i)!=value.FrontSoftened[i]||mask.IsSoftened(CoinSurfaceFace.Back,i)!=value.BackSoftened[i])throw new Exception("Mask mismatch "+i);
            Check(c.Dirt.All(d=>d.CurrentDurability==0),"A all removed restored");Check(true,"exact both-face B/P/softening CPU restore");
        }
        private void Capture(int n,string mode)
        {
            var camera=pointer.GameplayCamera;var rotation=coinTransform.rotation;var target=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(640,360,24);var image=new Texture2D(640,360,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;
                for(var f=0;f<2;f++)
                {
                    coinTransform.rotation=f==0?rotation:rotation*Quaternion.Euler(180,0,0);
                    camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();
                    var bytes=image.EncodeToPNG();File.WriteAllBytes(Path.Combine(directory,$"{mode}-{n}-{f}.png"),bytes);
                    using var hash=SHA256.Create();var digest=Convert.ToBase64String(hash.ComputeHash(image.GetRawTextureData()));
                    var path=Path.Combine(directory,$"render-{n}-{f}.txt");
                    if(mode=="seed")File.WriteAllText(path,digest);
                    else if(File.ReadAllText(path)==digest)Check(true,"same process-restart rendered face "+n+"/"+f);
                    else{renderMismatch=true;checks.Add("FAIL pixel restart "+n+"/"+f+" (data checks retained; diagnostic repeat captured)");}
                    var block=new MaterialPropertyBlock();coinTransform.GetComponentInChildren<MeshRenderer>().GetPropertyBlock(block);
                    var state=new StringBuilder();state.AppendLine("coin="+coinTransform.localToWorldMatrix.ToString("R"));state.AppendLine("camera="+camera.transform.localToWorldMatrix.ToString("R"));
                    state.AppendLine("projection="+camera.projectionMatrix.ToString("R"));
                    foreach(var key in new[]{"_Smoothness","_Metallic","_DirtySmoothness","_MaximumSmoothness","_ExperimentScanner","_SurfaceHalfHeight"})state.AppendLine(key+"="+block.GetFloat(key).ToString("R"));
                    foreach(var key in new[]{"_BaseColor","_DirtColor","_SurfaceBounds"})state.AppendLine(key+"="+block.GetVector(key).ToString("R"));
                    for(var channel=0;channel<3;channel++)for(var coefficient=0;coefficient<9;coefficient++)state.AppendLine("SH"+channel+"/"+coefficient+"="+RenderSettings.ambientProbe[channel,coefficient].ToString("R"));
                    File.WriteAllText(Path.Combine(directory,$"state-{mode}-{n}-{f}.txt"),state.ToString());
                    camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();
                    File.WriteAllBytes(Path.Combine(directory,$"repeat-{mode}-{n}-{f}.png"),image.EncodeToPNG());
                    checks.Add("within-frame repeat "+n+"/"+f+" equal="+(Convert.ToBase64String(hash.ComputeHash(image.GetRawTextureData()))==digest));
                    // CPU 저장 수치와 별개로 GPU texture byte 검사를 먼저 확정합니다.
                    var mask=f==0?surface.FrontDirtMask:surface.BackDirtMask;
                    var textureDigest=Convert.ToBase64String(hash.ComputeHash(mask.GetRawTextureData()));var texturePath=Path.Combine(directory,$"texture-{n}-{f}.txt");
                    if(mode=="seed")File.WriteAllText(texturePath,textureDigest);else Check(File.ReadAllText(texturePath)==textureDigest,"exact uploaded half-float texture bytes "+n+"/"+f);
                    // 동전 외 작업대/배경 조명은 제외합니다. 공유 에셋·정식 카메라 설정은 finally로 복원합니다.
                    var renderer=coinTransform.GetComponentInChildren<MeshRenderer>();var oldDebug=block.GetFloat("_SurfaceDebug");
                    var oldLayer=renderer.gameObject.layer;var oldMask=camera.cullingMask;var oldClear=camera.clearFlags;var oldBackground=camera.backgroundColor;var oldMsaa=camera.allowMSAA;
                    var additional=camera.GetUniversalAdditionalCameraData();var oldPost=additional.renderPostProcessing;
                    if(FindObjectsByType<Renderer>(FindObjectsSortMode.None).Any(other=>other!=renderer&&other.gameObject.layer==31))throw new InvalidOperationException("Validation isolation layer occupied");
                    try
                    {
                        renderer.gameObject.layer=31;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.allowMSAA=false;additional.renderPostProcessing=false;
                        block.SetFloat("_SurfaceDebug",1);renderer.SetPropertyBlock(block);camera.Render();RenderTexture.active=rt;
                        image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();
                        File.WriteAllBytes(Path.Combine(directory,$"mask-{mode}-{n}-{f}.png"),image.EncodeToPNG());
                        var debugDigest=Convert.ToBase64String(hash.ComputeHash(image.GetRawTextureData()));var debugPath=Path.Combine(directory,$"coin-mask-{n}-{f}.txt");
                        if(mode=="seed"||mode=="capture")File.WriteAllText(debugPath,debugDigest);else Check(File.ReadAllText(debugPath)==debugDigest,"exact coin-only shader sampled mask render "+n+"/"+f);
                    }
                    finally{block.SetFloat("_SurfaceDebug",oldDebug);renderer.SetPropertyBlock(block);renderer.gameObject.layer=oldLayer;camera.cullingMask=oldMask;camera.clearFlags=oldClear;camera.backgroundColor=oldBackground;camera.allowMSAA=oldMsaa;additional.renderPostProcessing=oldPost;}
                }
            }
            finally{coinTransform.rotation=rotation;camera.targetTexture=target;RenderTexture.active=active;Destroy(image);Destroy(rt);}
        }
        private static string Digest(ExperimentSnapshot value)
        {
            using var data=new MemoryStream();using var w=new BinaryWriter(data,Encoding.UTF8,true);
            w.Write(value.ResultId);w.Write(value.CoinId);w.Write(value.ReactionId);w.Write(value.LayoutId);w.Write(value.RoundId);w.Write(value.Resolution);w.Write(value.ElapsedSeconds);w.Write(value.InitialDirtSum);
            foreach(var array in new[]{value.A,value.FrontDirt,value.BackDirt,value.FrontPolish,value.BackPolish})foreach(var v in array)w.Write(v);
            foreach(var array in new[]{value.FrontSoftened,value.BackSoftened})foreach(var v in array)w.Write(v);
            using var hash=SHA256.Create();return Convert.ToBase64String(hash.ComputeHash(data.ToArray()));
        }
        private sealed class PostCommitFault:SaveFileOperations
        {
            private bool fired;
            public override void CommitNew(string temporary,string primary){base.CommitNew(temporary,primary);if(!fired){fired=true;throw new IOException("Injected after atomic move");}}
            public override void Replace(string temporary,string primary,string previous){base.Replace(temporary,primary,previous);if(!fired){fired=true;throw new IOException("Injected after atomic replace");}}
        }
        private sealed class DeniedWrite:SaveFileOperations
        {
            internal volatile bool Deny=true;
            public override void WriteNew(string path,byte[] bytes){if(Deny)throw new IOException("Injected temporary write denial");base.WriteNew(path,bytes);}
        }
        private sealed class DelayedWrite:SaveFileOperations
        {
            public override void WriteNew(string path,byte[] bytes){System.Threading.Thread.Sleep(200);base.WriteNew(path,bytes);}
        }
        private static bool ButtonText(string text)=>FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Any(b=>b.interactable&&b.GetComponentInChildren<UnityEngine.UI.Text>()?.text==text);
        private static void Click(string text)=>FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Single(b=>b.interactable&&b.GetComponentInChildren<UnityEngine.UI.Text>()?.text==text).onClick.Invoke();
        private void Finish(string result,bool automaticQuit=true)
        {
            ReleaseUiFiles();
            if(finished)return;finished=true;ReleaseFinalizationGate();ReleaseIntegrationGate();LastReport=result;Directory.CreateDirectory(directory);File.WriteAllLines(Path.Combine(directory,"probe-"+phase+".txt"),new[]{result}.Concat(checks));Debug.Log(result);
            if(!editorRun&&automaticQuit){if(c!=null)c.ApproveValidationQuit();Application.Quit(result.StartsWith("PASS",StringComparison.Ordinal)?0:1);}
        }
    }
}
#endif
