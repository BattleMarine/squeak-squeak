using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>문지르기 계약의 순수 프레임/실제 컴포넌트 API 검사. 물리 마우스 입력 인증은 아닙니다.</summary>
    public static class CleaningRubValidation
    {
        private static readonly Bounds Bounds = new(Vector3.zero,new Vector3(1,2,1));
        private static readonly Matrix4x4 Matrix = Matrix4x4.Scale(new Vector3(2,.1f,2));
        private const BindingFlags Private = BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool condition,string name,ref int checks)
        { if(!condition)throw new InvalidOperationException("s3-input FAIL: "+name);checks++; }
        private static CoinSurfaceContact Contact(float x,CoinSurfaceFace face=CoinSurfaceFace.Front,Matrix4x4? transform=null)
        {
            var matrix=transform??Matrix;
            var local=new Vector3(x,face==CoinSurfaceFace.Front?1:-1,0);
            if(!CoinSurfaceMapper.TryMapPoint(matrix,Bounds,matrix.MultiplyPoint3x4(local),matrix.inverse.transpose.MultiplyVector(face==CoinSurfaceFace.Front?Vector3.up:Vector3.down),out var c))throw new Exception("Contact mapping failed.");
            return c;
        }

        public static string RunData()
        {
            var checks=0; var surfaceKey=new object();var key=new object();var point=Contact(0);
            var motion=new CleaningRubMotion();
            Check(!motion.Sample(0,true,point,Matrix,key,surfaceKey,1f/60).IsRubbing,"최초 클릭0",ref checks);
            for(var frame=1;frame<=120;frame++)Check(!motion.Sample(frame,true,point,Matrix,key,surfaceKey,1f/60).IsRubbing,"정지0",ref checks);
            motion.Sample(121,false,point,Matrix,key,surfaceKey,1f/60);
            Check(!motion.Sample(122,true,point,Matrix,key,surfaceKey,1f/60).IsRubbing,"재클릭0",ref checks);
            var moved=motion.Sample(123,true,Contact(.005f),Matrix,key,surfaceKey,1f/60);
            Check(moved.IsRubbing&&Math.Abs(moved.EffectiveSeconds-.01f/.6f)<1e-6,"연속 이동 예산",ref checks);
            var again=motion.Sample(123,true,Contact(.1f),Matrix,key,surfaceKey,1);
            Check(again.EffectiveSeconds==moved.EffectiveSeconds&&again.End.WorldPosition==moved.End.WorldPosition,"같은 프레임 비소모/순서 독립",ref checks);

            foreach(var reason in new[]{"invalid","tool","surface","face","matrix","gap","explicit"})
            {
                motion=new CleaningRubMotion();motion.Sample(0,true,point,Matrix,key,surfaceKey,.01f);
                var matrix=Matrix;var tool=key;var surface=surfaceKey;var face=CoinSurfaceFace.Front;long next=1;
                switch(reason){case "invalid":motion.Sample(1,false,point,Matrix,key,surfaceKey,.01f);next=2;break;case "tool":tool=new object();break;case "surface":surface=new object();break;case "face":face=CoinSurfaceFace.Back;break;case "matrix":matrix=Matrix4x4.TRS(Vector3.up,Quaternion.Euler(35,0,0),new Vector3(2,.1f,2));break;case "gap":next=5;break;case "explicit":motion.BreakContact();break;}
                Check(!motion.Sample(next,true,Contact(.2f,face,matrix),matrix,tool,surface,.01f).IsRubbing,"단절 후 기준만: "+reason,ref checks);
            }
            foreach(var invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity})
                Check(!new CleaningRubMotion().Sample(0,true,point,Matrix,key,surfaceKey,invalid).IsRubbing,"잘못된 dt0",ref checks);

            double maxPixel=0,maxA=0,maxBudget=0;
            foreach(var name in new[]{"DryCloth","SmallBrush","PolishingCloth"})
            foreach(var mode in new[]{0,1,2,3}) // 정지 / 보통 / 느림 / 빠름
            {
                var tool=AssetDatabase.LoadAssetAtPath<CleaningToolDefinition>("Assets/Data/CleaningTools/"+name+".asset");
                (CoinSurfaceMaskData data,double budget,double a) Simulate(int hz)
                {
                    var sampler=new CleaningRubMotion();var data=new CoinSurfaceMaskData(64,.5f,CoinSurfaceDirtPattern.Uniform);var stroke=new CoinSurfaceBrushStroke();
                    var seconds=mode==2?8:1;var frames=seconds*hz;double budget=0;
                    for(var i=0;i<=frames;i++)
                    {
                        var x=mode==0?0:mode==2?-.02f+.005f*i/hz:mode==3?-.2f+.4f*i/hz:-.1f+.2f*i/hz;
                        var sample=sampler.Sample(i,true,Contact(x),Matrix,tool,surfaceKey,1f/hz);
                        Check(sampler.Sample(i,true,Contact(x),Matrix,tool,surfaceKey,1f/hz).EffectiveSeconds==sample.EffectiveSeconds,"공통 A/B 조회",ref checks);
                        if(!sample.IsRubbing)continue;
                        budget+=sample.EffectiveSeconds;
                        stroke.ApplyApprovedSweep(data,sample.Start,sample.End,Matrix,Bounds,tool,tool.BrushRadius,tool.SurfaceDirtRemovalPerSecond,sample.EffectiveSeconds,tool.PolishGainPerSecond);
                    }
                    return(data,budget,Math.Min(1,budget*tool.ObjectDirtRemovalPerSecond));
                }
                var reference=Simulate(120);
                foreach(var hz in new[]{30,60})
                {
                    var actual=Simulate(hz);
                    double pixel=0;
                    for(var i=0;i<actual.data.TexelCount;i++)pixel=Math.Max(pixel,Math.Max(Math.Abs(actual.data.GetDirt(CoinSurfaceFace.Front,i)-reference.data.GetDirt(CoinSurfaceFace.Front,i)),Math.Abs(actual.data.GetPolish(CoinSurfaceFace.Front,i)-reference.data.GetPolish(CoinSurfaceFace.Front,i))));
                    maxPixel=Math.Max(maxPixel,pixel);maxA=Math.Max(maxA,Math.Abs(actual.a-reference.a));maxBudget=Math.Max(maxBudget,Math.Abs(actual.budget-reference.budget));
                    Check(pixel<.015&&Math.Abs(actual.a-reference.a)<.003&&Math.Abs(actual.budget-reference.budget)<.002,"30/60/120Hz A/B/P "+name+"/"+mode,ref checks);
                    if(mode==0)Check(actual.budget==0&&actual.a==0&&actual.data.CurrentDirtSum==actual.data.InitialDirtSum&&actual.data.CurrentPolishSum==0,"세 도구 정지 모든 채널0",ref checks);
                    if(mode==3)Check(actual.budget<=1.00001,"빠른 이동 시간 상한",ref checks);
                }
            }
            motion=new CleaningRubMotion();motion.Sample(0,true,point,Matrix,key,surfaceKey,.01f);
            motion.Sample(1,true,Contact(.0002f),Matrix,key,surfaceKey,.01f);
            for(var i=2;i<102;i++)Check(!motion.Sample(i,true,Contact(.0002f),Matrix,key,surfaceKey,10).IsRubbing,"정지 시간 비누적",ref checks);
            var resumed=motion.Sample(102,true,Contact(.0006f),Matrix,key,surfaceKey,.01f);
            Check(resumed.IsRubbing&&resumed.EffectiveSeconds<.003,"느린 이동 누적/정지 후 예산",ref checks);
            return $"s3-input CPU PASS checks={checks}, maxPixel={maxPixel*100:F6}%p maxA={maxA*100:F6}%p maxEffectiveSeconds={maxBudget:F8}. 정지/초기/재클릭0, slow/fast/30/60/120Hz; 실제 입력 아님";
        }

        public static string RunPlay()
        {
            if(!Application.isPlaying||!EditorApplication.isPaused)throw new InvalidOperationException("Paused Play required.");
            var pointer=UnityEngine.Object.FindFirstObjectByType<CoinSurfacePointer>();
            var cleaner=pointer.GetComponent<ObjectDirtCleaner>();var progress=pointer.GetComponent<CleaningProgressTracker>();
            var owner=pointer.GetComponent<SqueakSqueak.Progression.CleaningStageController>();
            var surface=UnityEngine.Object.FindFirstObjectByType<CoinSurfaceContamination>();
            var selection=pointer.GetComponent<CleaningToolSelection>();var checks=0;
            var sampler=new CleaningRubMotion();var body=surface.GetComponent<MeshCollider>();var bounds=pointer.SurfaceLocalBounds;var matrix=pointer.SurfaceLocalToWorldMatrix;
            CoinSurfaceContact At(float x){var local=bounds.center+new Vector3(x,bounds.extents.y,0);if(!CoinSurfaceMapper.TryMapPoint(matrix,bounds,matrix.MultiplyPoint3x4(local),matrix.inverse.transpose.MultiplyVector(Vector3.up),out var c))throw new Exception("Mapping");return c;}
            void Frame(int frame,float x)
            {
                var contact=At(x);var sample=sampler.Sample(frame,true,contact,matrix,selection.CurrentTool,body,.01f);
                body.Raycast(new Ray(contact.WorldPosition+Vector3.up*4,Vector3.down),out var hit,10);
                typeof(CoinSurfacePointer).GetField("rubSample",Private).SetValue(pointer,sample);
                typeof(CoinSurfacePointer).GetField("rubHit",Private).SetValue(pointer,hit);
                typeof(CoinSurfacePointer).GetField("rubFrame",Private).SetValue(pointer,Time.frameCount);
                // 승인된 공통 프레임 패킷만 합성하며 실제 포인터/물리 입력 통과로 주장하지 않습니다.
                typeof(ObjectDirtCleaner).GetMethod("Update",Private).Invoke(cleaner,null);
                surface.ApplyCleaning(.01f);
                var sum=surface.SurfaceMask.CurrentDirtSum;var polish=surface.Polish;
                var dirtState=cleaner.RemovedDirtCount;
                typeof(ObjectDirtCleaner).GetMethod("Update",Private).Invoke(cleaner,null);
                Check(!surface.ApplyCleaning(.01f)&&sum==surface.SurfaceMask.CurrentDirtSum&&polish==surface.Polish&&dirtState==cleaner.RemovedDirtCount,"컴포넌트별 같은 패킷1회 적용",ref checks);
            }
            try
            {
                Check(owner.State==SqueakSqueak.Progression.CleaningStageState.Cleaning&&owner.StageId=="stage-01","새 초기 단계",ref checks);
                foreach(var dt in new[]{0f,-1f,float.NaN,float.PositiveInfinity})Check(!surface.ApplyCleaning(dt),"public API 잘못된 dt 거부",ref checks);
                var dirt=owner.GetComponentsInChildren<ObjectDirt>(true); // 오염은 동전 아래이므로 씬에서 정확한 활성 단계 배치를 찾습니다.
                dirt=pointer.gameObject.scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<ObjectDirt>(true)).Where(d=>d.gameObject.activeInHierarchy&&d.transform.parent.name=="StageDirt").ToArray();
                var before=dirt.Select(d=>d.CurrentDurability).ToArray();var initial=surface.SurfaceMask.CurrentDirtSum;
                for(var i=0;i<60;i++)Frame(i,-.15f);
                Check(surface.SurfaceMask.CurrentDirtSum==initial&&surface.Polish==0&&dirt.Select(d=>d.CurrentDurability).SequenceEqual(before)&&!progress.HasStarted,"공통 정지 프레임 A/B/P/타이머0",ref checks);
                for(var i=60;i<100;i++)Frame(i,-.15f+(i-59)*.002f);
                Check(surface.SurfaceMask.CurrentDirtSum<initial&&dirt.Where((d,i)=>d.CurrentDurability<before[i]).Any()&&progress.HasStarted,"이동 공통 프레임 A/B/타이머 적용",ref checks);
                var sum=surface.SurfaceMask.CurrentDirtSum;var polish=surface.Polish;var durability=dirt.Select(d=>d.CurrentDurability).ToArray();
                for(var i=100;i<160;i++)Frame(i,-.07f);
                Check(surface.SurfaceMask.CurrentDirtSum==sum&&surface.Polish==polish&&dirt.Select(d=>d.CurrentDurability).SequenceEqual(durability),"멈춤 즉시 모든 채널0",ref checks);
                pointer.ResetViewToInitialState();
                Check(!pointer.TryGetRubSample(out _,out _),"F 같은 프레임 캐시 취소",ref checks);
                foreach(var field in new[]{"minimumRubDistance","referenceRubSpeed"})Check(new SerializedObject(pointer).FindProperty(field)!=null,"Inspector 단위 설정 "+field,ref checks);
            }
            finally
            {
                if(owner.State==SqueakSqueak.Progression.CleaningStageState.Cleaning){owner.RequestAbandon(owner.RoundId);owner.ConfirmAbandon(owner.RoundId);owner.TryStartStage("stage-01",owner.RoundId);}
            }
            return "s3-input paused Scene API PASS checks="+checks+"; 공통 패킷 정지/이동/멈춤·A/B/P/타이머, 실제 마우스/UI/키 입력은 별도";
        }
    }
}
