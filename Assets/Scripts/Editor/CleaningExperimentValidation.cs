using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Experiment;

namespace SqueakSqueak.EditorValidation
{
    public static class CleaningExperimentValidation
    {
        private static readonly Bounds Bounds=new(Vector3.zero,new Vector3(1,2,1));
        private static readonly Matrix4x4 Matrix=Matrix4x4.Scale(new Vector3(2,.1f,2));
        private static void Check(bool value,string name,ref int checks){if(!value)throw new InvalidOperationException("Rb FAIL: "+name);checks++;}
        private static CoinSurfaceContact Contact(float x,float z=0,CoinSurfaceFace face=CoinSurfaceFace.Front)
        {
            var p=new Vector3(x,face==CoinSurfaceFace.Front?1:-1,z);
            if(!CoinSurfaceMapper.TryMapPoint(Matrix,Bounds,Matrix.MultiplyPoint3x4(p),face==CoinSurfaceFace.Front?Vector3.up:Vector3.down,out var contact))throw new Exception("Contact");
            return contact;
        }
        public static string RunData()
        {
            var definition=AssetDatabase.LoadAssetAtPath<CleaningExperimentDefinition>(CleaningExperimentTools.DefinitionPath);
            var checks=0;Check(definition!=null&&definition.Validate()==null,"실제 실험 정의",ref checks);
            Check((int)CleaningToolId.DryCloth==0&&(int)CleaningToolId.SmallBrush==1&&(int)CleaningToolId.PolishingCloth==2,"기존 enum ID",ref checks);
            var expected=new[]{new[]{1f,0f,0f,.5f,.25f,1f},new[]{1f,0f,0f,0f,1f,.25f}};
            for(var m=0;m<2;m++)
            {
                var profile=definition.Coins[m].ReactionProfile;
                var actual=new[]{profile.ObjectCoefficient(CleaningToolId.ExperimentToothbrush),profile.ObjectCoefficient(CleaningToolId.ExperimentCloth),
                    profile.SurfaceCoefficient(CleaningToolId.ExperimentToothbrush,false),profile.SurfaceCoefficient(CleaningToolId.ExperimentCloth,false),
                    profile.SurfaceCoefficient(CleaningToolId.ExperimentToothbrush,true),profile.SurfaceCoefficient(CleaningToolId.ExperimentCloth,true)};
                for(var i=0;i<6;i++)Check(actual[i]==expected[m][i],"반응표 "+m+"/"+i,ref checks);
                Check(profile.ObjectCoefficient(CleaningToolId.Dropper)==0&&profile.SurfaceCoefficient(CleaningToolId.Dropper,true)==0,"약품 제거0",ref checks);
            }
            var data=new CoinSurfaceMaskData(64,.8f,CoinSurfaceDirtPattern.Uniform);
            var initial=data.InitialDirtSum;var before=data.CurrentDirtSum;var revision=data.Revision;
            var changed=CoinSurfaceBrushStroke.SoftenDisk(data,Contact(-.08f),Matrix,Bounds,.18f);
            Check(changed>0&&data.CurrentDirtSum==before&&data.InitialDirtSum==initial&&data.CurrentPolishSum==0,"도포는 별도 상태만 변경",ref checks);
            Check(CoinSurfaceBrushStroke.SoftenDisk(data,Contact(-.08f),Matrix,Bounds,.18f)==0,"중복 도포 비중첩",ref checks);
            Check(Enumerable.Range(0,data.TexelCount).All(i=>!data.IsSoftened(CoinSurfaceFace.Back,i)),"반대면 도포 없음",ref checks);
            var outside=true;
            for(var i=0;i<data.TexelCount;i++)if(data.IsSoftened(CoinSurfaceFace.Front,i))
            {var x=i%64;var z=i/64;var delta=new Vector3(2*(x+.5f)/64-1+.16f,0,2*(z+.5f)/64-1);outside&=delta.sqrMagnitude<=.18f*.18f+1e-7f;}
            Check(outside,"풀림 실제 월드 반경",ref checks);
            var brush=definition.Tools.Single(t=>t.ToolId==CleaningToolId.ExperimentToothbrush);
            var stroke=new CoinSurfaceBrushStroke();
            Check(stroke.ApplyApprovedSweep(data,Contact(-.13f),Contact(.13f),Matrix,Bounds,brush,.18f,4.8f,.05f,0,definition.Coins[0].ReactionProfile,brush.ToolId),"풀린 B에 칫솔 반응",ref checks);
            Check(Enumerable.Range(0,data.TexelCount).Where(i=>data.IsValidTexel(i)&&!data.IsSoftened(CoinSurfaceFace.Front,i)).All(i=>data.GetDirt(CoinSurfaceFace.Front,i)==.8f),"미처리 영역에 풀림 효율 전파 금지",ref checks);
            var softenedIndex=Enumerable.Range(0,data.TexelCount).First(i=>data.IsSoftened(CoinSurfaceFace.Front,i));
            data.TrySetDirt(CoinSurfaceFace.Front,softenedIndex,0);
            Check(!data.IsSoftened(CoinSurfaceFace.Front,softenedIndex),"B0이면 풀림 해제",ref checks);
            Check(!data.TrySoften(CoinSurfaceFace.Front,softenedIndex)&&!data.TrySoften(CoinSurfaceFace.Side,10),"깨끗함/옆면 도포0",ref checks);
            data.Reset();Check(data.CurrentDirtSum==initial&&Enumerable.Range(0,data.TexelCount).All(i=>!data.IsSoftened(CoinSurfaceFace.Front,i)),"Reset B/풀림",ref checks);
            foreach(var material in definition.Coins)
            foreach(var tool in definition.Tools.Where(t=>!t.IsDropper))
            foreach(var soft in new[]{false,true})
            {
                var sample=new CoinSurfaceMaskData(32,.8f,CoinSurfaceDirtPattern.Uniform);
                if(soft)for(var i=0;i<sample.TexelCount;i++)sample.TrySoften(CoinSurfaceFace.Front,i);
                var sum=sample.CurrentDirtSum;var st=new CoinSurfaceBrushStroke();
                var result=st.ApplyApprovedSweep(sample,Contact(-.05f),Contact(.05f),Matrix,Bounds,tool,tool.BrushRadius,4.8f,.01f,0,material.ReactionProfile,tool.ToolId);
                Check(result==(material.ReactionProfile.SurfaceCoefficient(tool.ToolId,soft)>0),"실제 커널 유효/무효 반응",ref checks);
                Check(sample.CurrentPolishSum==0&&sample.GetCurrentDirtSum(CoinSurfaceFace.Back)==sample.GetInitialDirtSum(CoinSurfaceFace.Back),"광택/반대면 불변",ref checks);
                if(!result)Check(sample.CurrentDirtSum==sum,"무효 사용 변화0",ref checks);
            }
            var gate=new CleaningDropClickGate();Check(!gate.Sample(0,true,true),"복귀 Hold 도포0",ref checks);
            gate.Sample(1,false,true);Check(gate.Sample(2,true,true)&&gate.Sample(2,true,true),"클릭 한 샘플/비소모 조회",ref checks);
            for(var f=3;f<63;f++)Check(!gate.Sample(f,true,true),"Hold 연사0",ref checks);
            gate.Sample(63,false,true);gate.Sample(64,true,false);Check(!gate.Sample(65,true,true),"UI/포커스 등 차단 복귀 도포0",ref checks);
            gate.Sample(66,false,true);Check(gate.Sample(67,true,true),"놓고 새 클릭 허용",ref checks);
            gate.RequireRelease();Check(!gate.Sample(68,true,true),"도구/판 교체 Hold0",ref checks);
            Check(!new ExperimentProgress(false,0,0,0,0).IsComplete,"미초기화는 빈 완료 아님",ref checks);
            Check(new ExperimentProgress(true,0,0,0,0).IsComplete,"준비된 빈 동전",ref checks);
            Check(!new ExperimentProgress(true,2,1,0,0).IsComplete&&new ExperimentProgress(true,2,2,0,0).IsComplete,"A만 전부 제거",ref checks);
            Check(new ExperimentProgress(true,0,0,100,5).IsComplete&&!new ExperimentProgress(true,0,0,100,5.0002).IsComplete,"B95 경계/허용오차",ref checks);
            Check(new ExperimentProgress(true,0,0,1e-300,5e-302).IsComplete,"작은 초기 합 정규화",ref checks);
            var half=new ExperimentProgress(true,4,4,100,50);Check(Math.Abs(half.Value-.763157894736842)<1e-12,"단일 진행 계산",ref checks);
            Check(!new ExperimentProgress(true,4,2,100,50).IsComplete,"앞면만 완료 불가",ref checks);
            Check(new ExperimentProgress(true,1,1,100,5.001).DisplayPercent==99,"미완료 표시 반올림100 금지",ref checks);
            foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,-1d})
                Check(!new ExperimentProgress(true,0,0,invalid,0).IsValid&&!new ExperimentProgress(true,0,0,1,invalid).IsComplete,"불량 수치 거부",ref checks);
            data.TrySoften(CoinSurfaceFace.Front,softenedIndex);var a=new[]{0f};
            var snapshot=new ExperimentSnapshot(1,"rb-m1","rb-material-m1","rb-fixed-v1",data,a,2);
            var captured=snapshot.FrontDirt[softenedIndex];a[0]=100;data.TrySetDirt(CoinSurfaceFace.Front,softenedIndex,0);data.Reset();
            Check(snapshot.A[0]==0&&snapshot.FrontDirt[softenedIndex]==captured&&snapshot.FrontSoftened[softenedIndex],"보관 사본 원본 변경/Reset 불변",ref checks);
            var rejected=false;try{((System.Collections.Generic.IList<float>)snapshot.FrontDirt)[0]=.2f;}catch(NotSupportedException){rejected=true;}
            Check(rejected,"외부 수정 불가 컬렉션",ref checks);
            return "Rb DATA PASS checks="+checks+" (반응12/실제커널/국소풀림/스포이드gate/진행경계/불변사본)";
        }

        public static string RunPlay()
        {
            if(!Application.isPlaying||!EditorApplication.isPaused)throw new InvalidOperationException("새 CleaningRoom Play/Pause 필요");
            var c=UnityEngine.Object.FindFirstObjectByType<CleaningExperimentController>();var checks=0;
            var legacy=UnityEngine.Object.FindFirstObjectByType<SqueakSqueak.Progression.CleaningStageController>();
            var selection=legacy.GetComponent<CleaningToolSelection>();var tracker=legacy.GetComponent<CleaningProgressTracker>();
            var completion=legacy.GetComponent<CleaningCompletionController>();var pointer=legacy.GetComponent<CoinSurfacePointer>();
            var cleaner=legacy.GetComponent<ObjectDirtCleaner>();var surface=UnityEngine.Object.FindFirstObjectByType<CoinSurfaceContamination>();
            var definition=AssetDatabase.LoadAssetAtPath<CleaningExperimentDefinition>(CleaningExperimentTools.DefinitionPath);
            var normalTools=GameObject.Find("ToolSelectionCanvas").transform.Find("ToolPanel").gameObject;
            var guides=normalTools.transform.parent.Find("ControlsGuide").gameObject;
            var reached=0;var stored=0;void Goal(long _)=>reached++;void Store(ExperimentSnapshot _)=>stored++;
            c.GoalReached+=Goal;c.ResultStored+=Store;
            CoinSurfaceContact At(CoinSurfaceFace face)
            {
                var bounds=pointer.SurfaceLocalBounds;var matrix=pointer.SurfaceLocalToWorldMatrix;
                var local=bounds.center+new Vector3(-.16f,face==CoinSurfaceFace.Front?bounds.extents.y:-bounds.extents.y,.11f);
                if(!CoinSurfaceMapper.TryMapPoint(matrix,bounds,matrix.MultiplyPoint3x4(local),matrix.inverse.transpose.MultiplyVector(face==CoinSurfaceFace.Front?Vector3.up:Vector3.down),out var contact))throw new Exception("Scene mapping");return contact;
            }
            try
            {
                Check(!c.IsActive&&selection.AvailableToolCount==1&&legacy.enabled,"일반 시작 제한 보존",ref checks);
                Check(c.RequestCoin(0)&&c.IsActive&&c.State==ExperimentState.Cleaning,"명시적 M1 진입",ref checks);
                Check(!legacy.enabled&&!completion.EvaluationAllowed&&selection.AvailableToolCount==3&&!normalTools.activeSelf&&!guides.activeSelf,"소유자/일반 UI 분리",ref checks);
                Check(c.Dirt.Count==4&&cleaner.InitialDirtCount==4&&c.Dirt.All(d=>!d.IsRemoved),"양면 A4 초기화",ref checks);
                Check(surface.FrontDirtMask.format==TextureFormat.RGBAHalf&&surface.BackDirtMask.format==TextureFormat.RGBAHalf,"실험 정밀 마스크",ref checks);
                var sum=surface.SurfaceMask.CurrentDirtSum;var polish=surface.SurfaceMask.CurrentPolishSum;var revision=surface.SurfaceMask.Revision;var tool=selection.CurrentTool;
                Check(c.ToggleScanner()&&selection.CurrentTool==tool&&surface.SurfaceMask.Revision==revision&&!tracker.HasStarted,"스캐너는 표현만/타이머0",ref checks);
                Check(c.SelectTool(2),"스포이드 선택",ref checks);
                Check(surface.ApplyDroplet(At(CoinSurfaceFace.Front),.18f,1000000)&&tracker.HasStarted,"첫 유효 풀림 타이머 시작",ref checks);
                Check(surface.SurfaceMask.CurrentDirtSum==sum&&surface.SurfaceMask.CurrentPolishSum==polish,"실제 도포 제거/광택0",ref checks);
                Check(!surface.ApplyDroplet(At(CoinSurfaceFace.Front),.18f,1000000)&&!surface.ApplyDroplet(At(CoinSurfaceFace.Front),.18f,1000001),"동일 클릭/중복 국소 도포0",ref checks);
                Check(Enumerable.Range(0,surface.SurfaceMask.TexelCount).All(i=>!surface.SurfaceMask.IsSoftened(CoinSurfaceFace.Back,i)),"실제 반대면 불변",ref checks);
                var afterTreatment=surface.SurfaceMask.Revision;pointer.ResetViewToInitialState();
                Check(surface.SurfaceMask.Revision==afterTreatment&&surface.SurfaceMask.CurrentDirtSum==sum,"F 풀림/오염 보존",ref checks);
                c.enabled=false;var elapsed=tracker.ElapsedSeconds; // pause 경계까지 합산된 값을 기준으로 재개 보존을 검사합니다.
                Check(!pointer.enabled&&!cleaner.ProcessingAllowed&&!surface.ProcessingAllowed&&tracker.IsTimingPaused&&!c.SelectTool(0),"실험 소유자 비활성 입력/시간 차단",ref checks);
                c.enabled=true;Check(pointer.enabled&&!tracker.IsTimingPaused&&tracker.ElapsedSeconds==elapsed,"재활성 기존 판 재개",ref checks);
                foreach(var d in c.Dirt)d.ApplyCleaning(100);
                foreach(var face in new[]{CoinSurfaceFace.Front,CoinSurfaceFace.Back})
                for(var i=0;i<surface.SurfaceMask.TexelCount;i++)surface.SurfaceMask.TrySetDirt(face,i,surface.SurfaceMask.GetDirt(face,i)*.05f);
                surface.UploadSurfaceMasks();c.EvaluateGoal();c.EvaluateGoal();
                Check(c.State==ExperimentState.Ready&&reached==1&&pointer.enabled&&!tracker.IsTimingStopped,"목표1회·계속 청소/시간 허용",ref checks);
                var round=c.RoundId;Check(c.RequestStore(round)&&!pointer.enabled&&tracker.IsTimingPaused,"보관 확인 차단",ref checks);
                var paused=tracker.ElapsedSeconds;var current=surface.SurfaceMask.CurrentDirtSum;
                Check(!c.ConfirmDialog(round-1)&&c.CancelDialog(round)&&c.State==ExperimentState.Ready&&tracker.ElapsedSeconds==paused&&surface.SurfaceMask.CurrentDirtSum==current,"늦은 확인 거부/취소 보존",ref checks);
                var first=Enumerable.Range(0,surface.SurfaceMask.TexelCount).First(i=>surface.SurfaceMask.GetDirt(CoinSurfaceFace.Front,i)>0);
                surface.SurfaceMask.TrySetDirt(CoinSurfaceFace.Front,first,0);c.EvaluateGoal();
                Check(reached==1&&c.Progress.IsComplete&&pointer.enabled,"목표 후 추가 제거 가능",ref checks);
                Check(c.RequestStore(round)&&c.ConfirmDialog(round)&&c.State==ExperimentState.Stored&&stored==1,"최신 상태 메모리 보관",ref checks);
                var snapshot=c.CurrentResult;var copy=snapshot.FrontDirt.ToArray();var softened=snapshot.FrontSoftened.ToArray();
                Check(!c.ConfirmDialog(round)&&c.StoredResults.Count==1&&c.CurrentResult.ResultId==snapshot.ResultId,"중복 보관 거부",ref checks);
                Check(c.RequestCoin(1)&&c.RoundId>round&&c.CurrentCoin.ReactionProfile.Id==definition.Coins[1].ReactionProfile.Id,"다음 M2 새 판",ref checks);
                Check(snapshot.FrontDirt.SequenceEqual(copy)&&snapshot.FrontSoftened.SequenceEqual(softened)&&c.StoredResults[0]==snapshot,"다음 판 후 불변 결과",ref checks);
                Check(cleaner.RemovedDirtCount==0&&surface.SurfaceMask.CurrentDirtSum==surface.SurfaceMask.InitialDirtSum&&surface.Polish==0&&!c.ScannerEnabled&&!tracker.HasStarted,"M2 초기/광택/스캐너/시간 복원",ref checks);
                Check(Enumerable.Range(0,surface.SurfaceMask.TexelCount).All(i=>!surface.SurfaceMask.IsSoftened(CoinSurfaceFace.Front,i)&&!surface.SurfaceMask.IsSoftened(CoinSurfaceFace.Back,i)),"다음 판 풀림0",ref checks);
                var currentRound=c.RoundId;var initial=surface.SurfaceMask.CurrentDirtSum;
                Check(c.RequestCoin(0)&&c.State==ExperimentState.DiscardConfirmation&&c.CancelDialog(currentRound)&&c.RoundId==currentRound&&surface.SurfaceMask.CurrentDirtSum==initial,"새 동전 폐기 취소",ref checks);
                Check(c.RequestQuit()&&c.State==ExperimentState.QuitConfirmation&&!pointer.enabled&&c.CancelDialog(currentRound)&&c.State==ExperimentState.Cleaning,"종료 고지·취소 작업 유지",ref checks);
                var view=new CleaningExperimentView(UnityEngine.Object.FindFirstObjectByType<UnityEngine.Canvas>(),normalTools.GetComponentInChildren<UnityEngine.UI.Text>(true).font,c);
                try
                {
                    foreach(var value in new[]{0f,.5f,1f})
                    {view.Refresh(true,ExperimentState.Cleaning,(int)(value*100),0,0,16,"M1",selection.CurrentTool,false,definition.Tools,value);Check(view.GaugeAnchor==value,"게이지 실제 anchor 폭 "+value,ref checks);}
                }
                finally{view.Dispose();}
                Check(c.RequestLeave()&&c.ConfirmDialog(c.RoundId)&&!c.IsActive&&legacy.enabled&&selection.AvailableToolCount==1&&normalTools.activeSelf&&guides.activeSelf,"일반 판 복귀·소유 제한/UI 복원",ref checks);
            }
            finally
            {
                c.GoalReached-=Goal;c.ResultStored-=Store;
                if(c.IsActive&&(c.State==ExperimentState.Cleaning||c.State==ExperimentState.Ready)){c.RequestLeave();c.ConfirmDialog(c.RoundId);}
            }
            return "Rb Play API PASS checks="+checks+" (진입/양면/스캐너/유효풀림/수명/목표/확인취소/불변보관/다음/종료취소/게이지/일반복귀). 합성 상태·공개 커널 API이며 물리 입력 검사는 별도";
        }

        public static string PrepareLegacyCompletion()
        {
            if(!Application.isPlaying||!EditorApplication.isPaused)throw new InvalidOperationException("Play/Pause 필요");
            var c=UnityEngine.Object.FindFirstObjectByType<CleaningExperimentController>();
            var legacy=UnityEngine.Object.FindFirstObjectByType<SqueakSqueak.Progression.CleaningStageController>();
            if(c.IsActive||legacy.State!=SqueakSqueak.Progression.CleaningStageState.Cleaning)throw new InvalidOperationException("일반 Cleaning 판 필요");
            foreach(var d in legacy.gameObject.scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<ObjectDirt>(true)).Where(d=>d.transform.parent.name=="StageDirt"))d.ApplyCleaning(100);
            var surface=UnityEngine.Object.FindFirstObjectByType<CoinSurfaceContamination>();
            foreach(var face in new[]{CoinSurfaceFace.Front,CoinSurfaceFace.Back})
            for(var i=0;i<surface.SurfaceMask.TexelCount;i++)surface.SurfaceMask.TrySetDirt(face,i,0);
            surface.UploadSurfaceMasks();
            if(!legacy.GetComponent<CleaningCompletionController>().TryCompleteRound(legacy.StageId,legacy.RoundId))throw new InvalidOperationException("일반 완료 실패");
            return "일반 실제 완료 이벤트 → "+legacy.GetComponent<CleaningCompletionPresentation>().Phase;
        }

        public static string VerifyResultEntry(CompletionPresentationPhase expected)
        {
            var checks=0;var c=UnityEngine.Object.FindFirstObjectByType<CleaningExperimentController>();
            var presentation=UnityEngine.Object.FindFirstObjectByType<CleaningCompletionPresentation>();
            var canvas=GameObject.Find("ToolSelectionCanvas").transform;
            var tools=canvas.Find("ToolPanel").gameObject;var guide=canvas.Find("ControlsGuide").gameObject;
            Check(presentation.Phase==expected,"실제 연출 단계 "+expected,ref checks);
            Check(c.RequestCoin(0)&&c.State==ExperimentState.Cleaning,"Result → 실험",ref checks);
            Check(presentation.Phase==CompletionPresentationPhase.Idle&&!tools.activeSelf&&!guide.activeSelf,"연출 취소 뒤 일반 UI 비노출",ref checks);
            Check(c.RequestLeave()&&c.ConfirmDialog(c.RoundId)&&tools.activeSelf&&guide.activeSelf,"일반 UI 복원",ref checks);
            return "Rb Result 진입 PASS "+expected+" checks="+checks;
        }

        public static string VerifyGaugeGeometry()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play 필요");
            var checks=0;var canvas=UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var controller=UnityEngine.Object.FindFirstObjectByType<CleaningExperimentController>();
            var definition=AssetDatabase.LoadAssetAtPath<CleaningExperimentDefinition>(CleaningExperimentTools.DefinitionPath);
            var font=canvas.GetComponentInChildren<UnityEngine.UI.Text>(true).font;
            var view=new CleaningExperimentView(canvas,font,controller);
            try
            {
                var root=canvas.transform.GetChild(canvas.transform.childCount-2);
                var fill=root.Find("RB.Gauge/RB.GaugeFill").GetComponent<UnityEngine.UI.Image>();
                var track=(RectTransform)fill.transform.parent;
                foreach(var value in new[]{0f,.5f,1f})
                {
                    view.Refresh(true,ExperimentState.Cleaning,(int)(value*100),0,0,16,"M1",definition.Tools[0],false,definition.Tools,value);
                    Canvas.ForceUpdateCanvases();fill.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);var mesh=fill.canvasRenderer.GetMesh();
                    Check(Mathf.Abs(fill.rectTransform.rect.width-track.rect.width*value)<.001f,"게이지 실제 Rect 폭 "+value,ref checks);
                    Check(mesh.vertexCount>0&&Mathf.Abs(mesh.bounds.size.x-track.rect.width*value)<.001f,"게이지 제출 Mesh 폭 "+value,ref checks);
                }
            }
            finally{view.Dispose();}
            return "Rb 게이지 geometry PASS checks="+checks+" (0/50/100% 실제 Rect·CanvasRenderer Mesh 폭). 화면 시각 검사는 별도";
        }
    }
}
