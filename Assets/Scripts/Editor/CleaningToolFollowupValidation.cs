using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Progression;

namespace SqueakSqueak.EditorValidation
{
    public static class CleaningToolFollowupValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static string Run()
        {
            if (!Application.isPlaying || !EditorApplication.isPaused) throw new InvalidOperationException("새 CleaningRoom Play/Pause required.");
            var owner = UnityEngine.Object.FindFirstObjectByType<CleaningStageController>();
            if (owner == null || owner.StageId != "stage-01" || owner.State != CleaningStageState.Cleaning) throw new InvalidOperationException("초기 stage-01 required.");
            var selection = owner.GetComponent<CleaningToolSelection>();
            var buttons = UnityEngine.Object.FindObjectsByType<CleaningToolButton>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var tools = buttons.ToDictionary(b => (CleaningToolDefinition)typeof(CleaningToolButton).GetField("tool", Private).GetValue(b));
            var checks = 0;
            void Check(bool condition,string name) { if(!condition)throw new InvalidOperationException("s3-followup FAIL: "+name);checks++; }
            void Ui(bool all)
            {
                foreach(var pair in tools)
                {
                    var button=pair.Value.GetComponent<Button>();var label=pair.Value.GetComponentInChildren<Text>();
                    var available=all||pair.Key.ToolId==CleaningToolId.DryCloth;
                    Check(button.interactable==available&&selection.IsAvailable(pair.Key)==available,"UI/실제 선택 목록 일치");
                    Check(label.text.Contains("잠김")==!available,"잠금 표시");
                    if(!available)Check(label.text.Contains("미보유 도구")&&!selection.TrySelect(pair.Key),"미보유 사유/직접 선택 거부");
                    else Check(selection.TrySelect(pair.Key),"허용 도구 실제 선택");
                }
            }
            try
            {
                Check(selection.AvailableToolCount==1&&!CleaningToolTestMode.IsActive,"새 Play 정상 제한");Ui(false);
                var dirt=owner.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ObjectDirt>(true)).Where(d=>d.gameObject.activeInHierarchy).ToArray();
                Check(dirt.Length==2&&dirt.All(d=>d.GetComponent<Renderer>().shadowCastingMode==ShadowCastingMode.Off),"새 A2개 Cast Shadows Off");
                var surface=UnityEngine.Object.FindFirstObjectByType<CoinSurfaceContamination>();
                var sum=surface.SurfaceMask.CurrentDirtSum;var currentRound=owner.RoundId;
                CleaningToolTestMode.EnableAllTools();Ui(true);
                Check(CleaningToolTestMode.IsActive&&selection.AvailableToolCount==3&&owner.StageId=="stage-01"&&owner.RoundId==currentRound&&surface.SurfaceMask.CurrentDirtSum==sum,"검사 메뉴는 도구만 변경");
                CleaningToolTestMode.EnableAllTools();
                Check(selection.AvailableToolCount==3,"중복 메뉴 실행 안정");
                CleaningToolTestMode.RestoreNormalTools();Ui(false);
                Check(!CleaningToolTestMode.IsActive&&selection.AvailableToolCount==1,"명시 정상 제한 복원");
                var brush=tools.Keys.Single(t=>t.ToolId==CleaningToolId.SmallBrush);
                var dry=tools.Keys.Single(t=>t.ToolId==CleaningToolId.DryCloth);
                // 기본 선택이 그대로인 목록 변경도 잠금 UI에 알리는지를 검사합니다.
                selection.ConfigureAvailableTools(new[]{dry},new System.Collections.Generic.Dictionary<CleaningToolDefinition,string>{{brush,"이 단계 사용 불가"}});
                Check(tools[brush].GetComponentInChildren<Text>().text.Contains("이 단계 사용 불가"),"동일 기본 도구일 때 사유 이벤트 갱신");
                CleaningToolTestMode.EnableAllTools();
                Check(owner.RequestAbandon(owner.RoundId)&&owner.ConfirmAbandon(owner.RoundId)&&owner.TryStartStage("stage-01",owner.RoundId),"검사 모드 이후 새 판");
                Ui(false);Check(!CleaningToolTestMode.IsActive,"새 판 검사 모드 만료");
                CleaningToolTestMode.RestoreNormalTools();Ui(false);
                Check(selection.AvailableToolCount==1,"옛 검사 판 목록 복원 금지");
                var selectedCount=Listeners(selection,"ToolSelected");var availableCount=Listeners(selection,"AvailableToolsChanged");
                foreach(var b in buttons){b.enabled=false;b.enabled=true;}
                Check(Listeners(selection,"ToolSelected")==selectedCount&&Listeners(selection,"AvailableToolsChanged")==availableCount,"버튼 재활성 구독 누적 없음");
                Ui(false);
            }
            finally { CleaningToolTestMode.RestoreNormalTools(); selection.TrySelect(CleaningToolId.DryCloth); }
            return "s3-followup paused Play API PASS checks="+checks+"; 잠금/사유/직접선택/개발 메뉴3도구/복원/새 판/구독/그림자";
        }

        private static int Listeners(CleaningToolSelection selection,string field)=>(typeof(CleaningToolSelection).GetField(field,Private).GetValue(selection) as Delegate)?.GetInvocationList().Length??0;

        /// <summary>Overlay UI도 같은 실제 카메라로 확인합니다. 임시 Canvas 설정은 finally에서 복원하고 저장하지 않습니다.</summary>
        public static string CaptureUi(string path)
        {
            if(!Application.isPlaying||!EditorApplication.isPaused)throw new InvalidOperationException("Paused Play required.");
            var camera=Camera.main;var canvas=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(c=>c.isRootCanvas);
            var mode=canvas.renderMode;var previousCamera=canvas.worldCamera;var distance=canvas.planeDistance;
            try
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.1f;
                Canvas.ForceUpdateCanvases();
                CoinSurfaceRenderingValidation.Capture(camera,path,1280,720);
                return path;
            }
            finally { canvas.renderMode=mode;canvas.worldCamera=previousCamera;canvas.planeDistance=distance;Canvas.ForceUpdateCanvases(); }
        }
    }
}
