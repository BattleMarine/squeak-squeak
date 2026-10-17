using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Experiment;
using SqueakSqueak.Saving;
using SqueakSqueak.Progression;

namespace SqueakSqueak.EditorValidation
{
    public static class CoinSaveValidation
    {
        public const string RenderRevision="cleaning-room-rb-v1";
        private static int checks;
        private static void Check(bool value,string name){if(!value)throw new InvalidOperationException("S3-03a FAIL: "+name);checks++;}
        private static void Reject(Action action,SaveIssue issue,string name)
        {var rejected=false;try{action();}catch(SaveDataException e){rejected=e.Issue==issue;}Check(rejected,name);}
        private static string Hash(byte[] data){using var sha=SHA256.Create();return Convert.ToBase64String(sha.ComputeHash(data));}
        private static string Fingerprint(string path)=>Hash(File.ReadAllBytes(path));
        public static CoinSaveRenderContract ActualRenderContract(string revision=RenderRevision)
        {
            var surface=UnityEngine.Object.FindFirstObjectByType<CoinSurfaceContamination>();if(surface==null)throw new InvalidOperationException("CleaningRoom rendering reference required");
            var so=new SerializedObject(surface);return new CoinSaveRenderContract(revision,so.FindProperty("dirtColor").colorValue,so.FindProperty("dirtySmoothness").floatValue,so.FindProperty("maximumSmoothness").floatValue);
        }
        private static byte[] Gzip(byte[] bytes){using var result=new MemoryStream();using(var gzip=new GZipStream(result,System.IO.Compression.CompressionLevel.Optimal,true))gzip.Write(bytes,0,bytes.Length);return result.ToArray();}
        private static byte[] Rewrite(byte[] file,Action<byte[]> change)
        {
            using var packed=new MemoryStream(file,56,file.Length-56,false);using var gzip=new GZipStream(packed,CompressionMode.Decompress);using var raw=new MemoryStream();gzip.CopyTo(raw);
            var payload=raw.ToArray();change(payload);var compressed=Gzip(payload);using var output=new MemoryStream();using var w=new BinaryWriter(output);
            w.Write(file.Take(16).ToArray());w.Write(payload.Length);w.Write(compressed.Length);using var sha=SHA256.Create();w.Write(sha.ComputeHash(payload));w.Write(compressed);return output.ToArray();
        }
        private static void Int(byte[] bytes,int index,int value){var v=BitConverter.GetBytes(value);Array.Copy(v,0,bytes,index,4);}
        private static void ReplaceText(byte[] bytes,string old,string value)
        {var a=Encoding.UTF8.GetBytes(old);var b=Encoding.UTF8.GetBytes(value);if(a.Length!=b.Length)throw new Exception("Test length");for(var i=0;i<=bytes.Length-a.Length;i++)if(bytes.Skip(i).Take(a.Length).SequenceEqual(a)){Array.Copy(b,0,bytes,i,b.Length);return;}throw new Exception("Test text missing");}
        public static SavedCoinDto Sample(CoinSaveDefinitionCatalog definitions,string coin="rb-coin-m1",int resolution=256)
        {
            var contract=definitions.Get(coin);var mask=contract.InitialMask(resolution);var random=new System.Random(913);
            foreach(var face in new[]{CoinSurfaceFace.Front,CoinSurfaceFace.Back})for(var i=0;i<mask.TexelCount;i++)
            {
                // 최대 약3.8% 잔여. 완성 뒤 더 닦은 비균일 표면/풀림을 보존하는 표본입니다.
                var value=mask.GetDirt(face,i)*(i%20<3?(float)(random.NextDouble()*.5):0);mask.TrySetDirt(face,i,value);
                if(i%3==0)mask.TrySoften(face,i);
            }
            var snapshot=new ExperimentSnapshot(7,contract.CoinId,contract.ReactionId,contract.LayoutId,mask,new float[contract.ACount],123.4567f);
            var a=SavedCoinDto.FromSnapshot(snapshot,definitions);Check(a.resultId==snapshot.ResultId,"snapshot result identity retained");return a;
        }
        private enum Fault { WriteDenied,PartialWrite,ClosedWrite,ChangedTemporary,CommitBefore,CommitAfter,ReadDenied }
        private sealed class FaultFiles:SaveFileOperations
        {
            private readonly Fault fault;internal FaultFiles(Fault fault){this.fault=fault;}
            public override byte[] Read(string path)
            {if(fault==Fault.ReadDenied)throw new UnauthorizedAccessException("Injected read denial");var bytes=base.Read(path);if(fault==Fault.ChangedTemporary&&path.Contains(".tmp-"))bytes[bytes.Length/2]^=1;return bytes;}
            public override void WriteNew(string path,byte[] bytes)
            {
                if(fault==Fault.WriteDenied)throw new UnauthorizedAccessException("Injected write denial");
                if(fault==Fault.PartialWrite){using var stream=new FileStream(path,FileMode.CreateNew);stream.Write(bytes,0,bytes.Length/2);stream.Flush(true);throw new IOException("Injected partial write");}
                base.WriteNew(path,bytes);if(fault==Fault.ClosedWrite)throw new IOException("Injected post-close failure");
            }
            public override void Replace(string temporary,string primary,string previous)
            {if(fault==Fault.CommitBefore)throw new IOException("Injected pre-replace failure");base.Replace(temporary,primary,previous);if(fault==Fault.CommitAfter)throw new IOException("Injected post-replace uncertainty");}
            public override void CommitNew(string temporary,string primary)
            {if(fault==Fault.CommitBefore)throw new IOException("Injected pre-move failure");base.CommitNew(temporary,primary);if(fault==Fault.CommitAfter)throw new IOException("Injected post-move uncertainty");}
        }
        [MenuItem("Squeak Squeak/Validation/S3-03a Isolated Save Store")]
        private static void Menu()=>UnityEngine.Debug.Log(Run());
        public static string Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit mode required; no user save access");checks=0;
            var definition=AssetDatabase.LoadAssetAtPath<CleaningExperimentDefinition>(CleaningExperimentTools.DefinitionPath);
            var render=ActualRenderContract();var definitions=new CoinSaveDefinitionCatalog(definition,render);var codec=new CompletedCoinCodec(definitions);var validator=new CoinSaveValidator(definitions);
            var root=Path.GetFullPath(".utmp/s303a-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            string Area(string name){var area=Path.Combine(root,name);Directory.CreateDirectory(area);return area;}
            var one=Sample(definitions);var two=Sample(definitions,"rb-coin-m2");var three=Sample(definitions);var library=new CompletedCoinLibraryDto{formatVersion=1,results=new[]{one}};
            var bytes=codec.Encode(library,1);var roundtrip=codec.Decode(bytes);
            Check(roundtrip.Revision==1&&codec.SameResult(one,roundtrip.Data.results[0]),"all float/bool/ID bytes roundtrip");
            var area=Area("normal");var store=new CompletedCoinStore(area,definitions);var primary=Path.Combine(area,CompletedCoinStore.FileName);var backup=primary+".bak";
            Check(store.Load().Issue==SaveIssue.Missing&&!File.Exists(primary),"missing does not create/restore unfinished work");
            Check(store.Save(one).Status==SaveWriteStatus.Saved,"first durable save");var first=File.ReadAllBytes(primary);var firstHash=Fingerprint(primary);
            Check(store.Save(one).Status==SaveWriteStatus.AlreadySaved&&Fingerprint(primary)==firstHash&&store.Load().Data.results.Length==1,"same ID retry no write/duplicate");
            var conflict=one.Copy();conflict.elapsedSeconds++;Check(store.Save(conflict).Status==SaveWriteStatus.Conflict&&Fingerprint(primary)==firstHash,"different payload same ID conflict");
            Check(new CompletedCoinStore(area,definitions).Save(two).Succeeded&&store.Load().Revision==2&&File.ReadAllBytes(backup).SequenceEqual(first),"new store preserves previous result and backup");
            var corrupt=new byte[]{1,2,3};File.WriteAllBytes(primary,corrupt);var recovered=store.Load();
            Check(recovered.HasData&&recovered.Source==SaveSource.Backup&&recovered.Data.results.Length==1&&File.ReadAllBytes(primary).SequenceEqual(corrupt),"read valid backup without destroying corrupt main");
            Check(store.Save(three).Succeeded&&File.ReadAllBytes(backup).SequenceEqual(first)&&store.Load().Data.results.Length==2,"recovery save keeps sole valid backup");
            Check(Directory.GetFiles(area,"*.corrupt-*").Any(p=>File.ReadAllBytes(p).SequenceEqual(corrupt)),"corrupt main preserved separately");
            File.WriteAllBytes(backup,corrupt);var fourth=Sample(definitions);Check(store.Save(fourth).Succeeded&&Directory.GetFiles(area,"*.corrupt-*").Length==2,"corrupt backup preserved before rotation");

            foreach(var fault in Enum.GetValues(typeof(Fault)).Cast<Fault>())
            {
                var dir=Area("fault-"+fault);var target=Path.Combine(dir,CompletedCoinStore.FileName);var normal=new CompletedCoinStore(dir,definitions);Check(normal.Save(one).Succeeded,"fault setup");
                var before=Fingerprint(target);var broken=new CompletedCoinStore(dir,definitions,new FaultFiles(fault));var result=broken.Save(two);
                Check(!result.Succeeded,"failure is not success "+fault);
                if(fault==Fault.ReadDenied)Check(broken.Load().Issue==SaveIssue.IoError,"read access failure distinguished from physical corruption");
                if(fault==Fault.CommitAfter){Check(result.Status==SaveWriteStatus.Indeterminate&&normal.Save(two).Status==SaveWriteStatus.AlreadySaved&&normal.Load().Data.results.Length==2,"uncertain commit retry stable ID");}
                else
                {Check(Fingerprint(target)==before&&normal.Load().Data.results.Length==1,"failure preserves committed file "+fault);Check(normal.Save(two).Succeeded&&normal.Load().Data.results.Length==2,"safe retry "+fault);}
            }
            foreach(var fault in new[]{Fault.PartialWrite,Fault.ClosedWrite,Fault.CommitBefore,Fault.CommitAfter})
            {
                var dir=Area("first-"+fault);var status=new CompletedCoinStore(dir,definitions,new FaultFiles(fault)).Save(one);Check(!status.Succeeded,"initial failure not success");
                var normal=new CompletedCoinStore(dir,definitions);var orphan=Directory.GetFiles(dir,"*.tmp-*");
                if(fault!=Fault.CommitAfter)Check(normal.Load().Issue==SaveIssue.Missing,"uncommitted temporary never promoted");
                Check(normal.Save(one).Succeeded&&normal.Load().Data.results.Length==1&&orphan.All(File.Exists),"initial failure retry no duplicate/orphans preserved");
            }
            var locked=Area("locked");var lockedStore=new CompletedCoinStore(locked,definitions);Check(lockedStore.Save(one).Succeeded,"lock setup");
            using(var lease=new FileStream(Path.Combine(locked,CompletedCoinStore.FileName)+".lock",FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(lockedStore.Save(two).Status==SaveWriteStatus.Failed,"exclusive writer lock");

            void Blocked(byte[] main,byte[] old,SaveIssue expected,string name)
            {
                var dir=Area(name);var file=Path.Combine(dir,CompletedCoinStore.FileName);if(main!=null)File.WriteAllBytes(file,main);if(old!=null)File.WriteAllBytes(file+".bak",old);
                var s=new CompletedCoinStore(dir,definitions);var initial=main==null?null:Fingerprint(file);var previous=old==null?null:Fingerprint(file+".bak");
                Check(s.Load().Issue==expected&&s.Save(two).Status==SaveWriteStatus.Rejected,"blocked "+name);
                Check((main==null?!File.Exists(file):Fingerprint(file)==initial)&&(old==null?!File.Exists(file+".bak"):Fingerprint(file+".bak")==previous),"blocked files preserved "+name);
            }
            var future=(byte[])bytes.Clone();Int(future,8,2);Blocked(future,bytes,SaveIssue.Unsupported,"future-main");Blocked(bytes,future,SaveIssue.Unsupported,"future-backup");
            Blocked(future.Take(12).ToArray(),bytes,SaveIssue.Unsupported,"short-future-envelope");
            Blocked(corrupt,corrupt,SaveIssue.Corrupt,"both-corrupt");Blocked(Array.Empty<byte>(),null,SaveIssue.Corrupt,"empty-main");Blocked(null,corrupt,SaveIssue.Corrupt,"only-bad-backup");
            var unknown=Rewrite(bytes,p=>ReplaceText(p,"rb-coin-m1","xx-coin-m1"));Blocked(unknown,bytes,SaveIssue.UnknownDefinition,"unknown-id");
            var changed=Rewrite(bytes,p=>ReplaceText(p,one.definitionFingerprint,new string('a',64)));Blocked(changed,bytes,SaveIssue.DefinitionChanged,"changed-definition");
            Blocked(bytes,changed,SaveIssue.DefinitionChanged,"changed-backup");Blocked(bytes,unknown,SaveIssue.UnknownDefinition,"unknown-backup");
            var innerFuture=Rewrite(bytes,p=>Int(p,16,2));Blocked(innerFuture,bytes,SaveIssue.Unsupported,"future-result");
            var oversize=(byte[])bytes.Clone();Int(oversize,16,CoinSaveLimits.MaximumDecodedBytes+1);Blocked(oversize,null,SaveIssue.Corrupt,"oversize-header");
            var largeDirectory=Area("oversize-file");var largeFile=Path.Combine(largeDirectory,CompletedCoinStore.FileName);
            using(var file=new FileStream(largeFile,FileMode.CreateNew))file.SetLength(CoinSaveLimits.MaximumFileBytes+1L);
            File.WriteAllBytes(largeFile+".bak",bytes);var tooLarge=new CompletedCoinStore(largeDirectory,definitions);
            Check(tooLarge.Load().Issue==SaveIssue.Unsupported&&tooLarge.Save(two).Status==SaveWriteStatus.Rejected&&new FileInfo(largeFile).Length==CoinSaveLimits.MaximumFileBytes+1L,"oversize file cannot be downgraded/overwritten");

            void Invalid(Action<SavedCoinDto> edit,SaveIssue issue,string name){var v=one.Copy();edit(v);Reject(()=>validator.ValidateResult(v),issue,name);}
            Invalid(v=>v.frontDirt=new float[2],SaveIssue.Corrupt,"array length");Invalid(v=>v.a=null,SaveIssue.Corrupt,"missing A");Invalid(v=>v.a[0]=.1f,SaveIssue.Corrupt,"unfinished A");
            Invalid(v=>v.frontDirt[100]=float.NaN,SaveIssue.Corrupt,"NaN dirt");Invalid(v=>v.backPolish[100]=float.PositiveInfinity,SaveIssue.Corrupt,"infinite polish");Invalid(v=>v.frontDirt[100]=-1,SaveIssue.Corrupt,"negative dirt");
            Invalid(v=>v.frontSoftened[0]=true,SaveIssue.Corrupt,"outside softened");Invalid(v=>v.frontPolish[0]=.1f,SaveIssue.Corrupt,"polish excluded");Invalid(v=>v.elapsedSeconds=float.NaN,SaveIssue.Corrupt,"invalid time");
            Invalid(v=>v.initialDirtSum++,SaveIssue.Corrupt,"initial denominator");Invalid(v=>v.resolution=int.MaxValue,SaveIssue.Corrupt,"allocation resolution bound");Invalid(v=>v.coinVersion++,SaveIssue.DefinitionChanged,"definition version");
            Invalid(v=>v.renderRevision="new-render-v2",SaveIssue.DefinitionChanged,"render semantics version");Invalid(v=>v.resultId=new string('x',1000),SaveIssue.Corrupt,"invalid identity");
            Invalid(v=>{var initial=definitions.Get(v.coinId).InitialMask(v.resolution);for(var i=0;i<initial.TexelCount;i++){v.frontDirt[i]=initial.GetDirt(CoinSurfaceFace.Front,i);v.backDirt[i]=initial.GetDirt(CoinSurfaceFace.Back,i);}},SaveIssue.Corrupt,"unfinished B despite removed A");
            Invalid(v=>{var initial=definitions.Get(v.coinId).InitialMask(v.resolution);var i=Enumerable.Range(0,v.frontDirt.Length).First(i=>initial.IsValidTexel(i)&&v.frontDirt[i]==0);v.frontSoftened[i]=true;},SaveIssue.Corrupt,"softened zero dirt");
            Reject(()=>codec.Encode(new CompletedCoinLibraryDto{formatVersion=1,results=new[]{one,one}},1),SaveIssue.Corrupt,"duplicate IDs");
            Reject(()=>codec.Decode(bytes.Take(bytes.Length-1).ToArray()),SaveIssue.Corrupt,"truncation");var wrong=(byte[])bytes.Clone();wrong[24]^=1;Reject(()=>codec.Decode(wrong),SaveIssue.Corrupt,"digest");

            var culture=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=new CultureInfo("tr-TR");Check(new CoinSaveDefinitionCatalog(definition,render).Get(one.coinId).Fingerprint==one.definitionFingerprint,"locale independent fingerprint");}finally{CultureInfo.CurrentCulture=culture;}
            var newContract=new CoinSaveDefinitionCatalog(definition,ActualRenderContract("render-v2"));Reject(()=>new CompletedCoinCodec(newContract).Decode(bytes),SaveIssue.DefinitionChanged,"new runtime refuses old rendering contract");
            var changedRender=new CoinSaveRenderContract(render.Revision,render.DirtColor*.9f,render.DirtySmoothness,render.MaximumSmoothness);
            Reject(()=>new CompletedCoinCodec(new CoinSaveDefinitionCatalog(definition,changedRender)).Decode(bytes),SaveIssue.DefinitionChanged,"actual renderer override changed");
            var definitionClone=UnityEngine.Object.Instantiate(definition);var coinClone=UnityEngine.Object.Instantiate(definition.Coins[0]);
            var materialClone=UnityEngine.Object.Instantiate(coinClone.SurfaceMaterial);materialClone.name=coinClone.SurfaceMaterial.name;
            var meshClone=UnityEngine.Object.Instantiate(coinClone.SurfaceMesh);meshClone.name=coinClone.SurfaceMesh.name;
            try
            {
                var dso=new SerializedObject(definitionClone);dso.FindProperty("coins").GetArrayElementAtIndex(0).objectReferenceValue=coinClone;dso.ApplyModifiedPropertiesWithoutUndo();
                var cso=new SerializedObject(coinClone);cso.FindProperty("surfaceMaterial").objectReferenceValue=materialClone;cso.ApplyModifiedPropertiesWithoutUndo();
                materialClone.SetFloat("_Smoothness",.123f);
                Check(new CoinSaveDefinitionCatalog(definitionClone,render).Get(one.coinId).Fingerprint!=one.definitionFingerprint,"same-name material different values detected");
                cso.Update();cso.FindProperty("surfaceMaterial").objectReferenceValue=definition.Coins[0].SurfaceMaterial;cso.FindProperty("surfaceMesh").objectReferenceValue=meshClone;cso.ApplyModifiedPropertiesWithoutUndo();
                var triangles=meshClone.triangles;for(var i=0;i<3;i++){var swap=triangles[i];triangles[i]=triangles[i+3];triangles[i+3]=swap;}meshClone.triangles=triangles;
                Check(new CoinSaveDefinitionCatalog(definitionClone,render).Get(one.coinId).Fingerprint!=one.definitionFingerprint,"same-name mesh content detected");
            }
            finally{UnityEngine.Object.DestroyImmediate(definitionClone);UnityEngine.Object.DestroyImmediate(coinClone);UnityEngine.Object.DestroyImmediate(materialClone);UnityEngine.Object.DestroyImmediate(meshClone);}

            var progression=AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(ProgressionCatalogTools.CatalogPath);var progressDefinitions=new GeneralProgressSaveDefinition(progression);var progress=progressDefinitions.InitialProgress();var progressStore=new GeneralProgressStore(area,progressDefinitions);
            Check(progressStore.Load().Issue==SaveIssue.Missing&&progressStore.Save(progress,0).Succeeded&&progressStore.Load().Revision==1,"separate general progress file");
            Check(progressStore.Save(progress,0).Status==SaveWriteStatus.RevisionConflict,"stale progress snapshot rejected");
            progress.ownedToolIds=new[]{"rb-dropper"};Reject(()=>progressDefinitions.Validate(progress),SaveIssue.UnknownDefinition,"experiment access not persisted as ownership");
            Check(store.Load().Data.results.Length==3,"general progress cannot overwrite result file");
            progress=progressDefinitions.InitialProgress();progress.ownedToolIds=new[]{"dry-cloth","dry-cloth"};Reject(()=>progressDefinitions.Validate(progress),SaveIssue.Corrupt,"duplicate ownership");
            progress=progressDefinitions.InitialProgress();progress.selectedStageId="stage-02";Reject(()=>progressDefinitions.Validate(progress),SaveIssue.Corrupt,"unearned selected stage");
            var workerDirectory=Area("worker");var progressCopy=progressDefinitions.InitialProgress();
            var workerOk=System.Threading.Tasks.Task.Run(()=>
            {
                var results=new CompletedCoinStore(workerDirectory,definitions);var general=new GeneralProgressStore(workerDirectory,progressDefinitions);
                return results.Save(one).Succeeded&&general.Save(progressCopy,0).Succeeded&&codec.SameResult(one,results.Load().Data.results[0])&&general.Load().Data.ownedToolIds.SequenceEqual(progressCopy.ownedToolIds);
            }).GetAwaiter().GetResult();Check(workerOk,"worker uses frozen definitions/DTO/IO only");

            var maximum=new CompletedCoinLibraryDto{formatVersion=1,results=Enumerable.Range(0,16).Select(i=>{var v=one.Copy();v.resultId=Guid.NewGuid().ToString("N");return v;}).ToArray()};
            var capDir=Area("capacity");File.WriteAllBytes(Path.Combine(capDir,CompletedCoinStore.FileName),codec.Encode(maximum,9));
            var cap=new CompletedCoinStore(capDir,definitions);Check(cap.Save(two).Status==SaveWriteStatus.Full&&cap.Load().Data.results.Length==16,"capacity preserves all16");
            Check(cap.Save(maximum.results[0]).Status==SaveWriteStatus.AlreadySaved,"retry allowed at capacity");
            var timer=Stopwatch.StartNew();var packedMaximum=codec.Encode(maximum,9);var encodeMs=timer.Elapsed.TotalMilliseconds;timer.Restart();var decoded=codec.Decode(packedMaximum);var decodeMs=timer.Elapsed.TotalMilliseconds;
            Check(decoded.Data.results.Length==16&&codec.SameResult(maximum.results[15],decoded.Data.results[15]),"maximum full payload roundtrip");
            var json=Encoding.UTF8.GetBytes(JsonUtility.ToJson(library));var gzipJson=Gzip(json);var jsonDto=JsonUtility.FromJson<CompletedCoinLibraryDto>(Encoding.UTF8.GetString(json));
            var report=$"S3-03a PASS checks={checks}; artifacts={root}\nres256 sample: JSON={json.Length}B GZipJSON={gzipJson.Length}B GZipBinaryEnvelope={bytes.Length}B JSON-bitexact={codec.SameResult(one,jsonDto.results[0])}; binary-bitexact=true\n16 results binary={packedMaximum.Length}B encode={encodeMs:F2}ms decode+validate={decodeMs:F2}ms (Editor/Mono sample, not Player budget); no persistentDataPath access";
            File.WriteAllText(Path.Combine(root,"report.txt"),report);return report;
        }
        public static string VerifyPreviousRun(string absoluteTestDirectory)
        {
            var testRoot=Path.GetFullPath(absoluteTestDirectory);var allowed=Path.GetFullPath(".utmp")+Path.DirectorySeparatorChar;
            if(!testRoot.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(testRoot))throw new ArgumentException("Existing isolated .utmp test directory required");
            var definition=AssetDatabase.LoadAssetAtPath<CleaningExperimentDefinition>(CleaningExperimentTools.DefinitionPath);
            var definitions=new CoinSaveDefinitionCatalog(definition,ActualRenderContract());var read=new CompletedCoinStore(Path.Combine(testRoot,"normal"),definitions).Load();
            if(!read.HasData||read.Source!=SaveSource.Primary||read.Data.results.Length!=3)throw new InvalidOperationException("Previous domain/cold definitions could not read prior bytes: "+read.Issue);
            return "S3-03a previous-run bytes PASS: new definition catalog/fingerprint compatible; count3. Editor domain reload only, not Player/process restart.";
        }
    }
}
