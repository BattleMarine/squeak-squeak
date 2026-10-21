#if UNITY_EDITOR || SQUEAK_SAVE_VALIDATION
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Experiment;

namespace SqueakSqueak.Saving
{
    public sealed partial class SavePlayerProbe
    {
        private FileStream uiLock;
        private UiDelayedWrite uiFiles;
        private IEnumerator RunUiFlow()
        {
            while(c.PersistenceBusy)yield return null;
            Check(c.SaveReady,"UI load ready");
            if(phase=="ui-capacity")
            {
                Check(c.StoredResults.Count==16&&c.RequestLibrary(),"16 result library UI opens");UiCapture("16-library");
                Check(FindObjectsByType<Button>(FindObjectsSortMode.None).Count(x=>x.name=="RB.Button.")==16,"all16 list rows active");
                Click("목록 닫기");Check(c.RequestCoin(0),"work allowed at capacity");Complete(0);
                Check(c.RequestStore(c.RoundId)&&!c.ConfirmDialog(c.RoundId),"capacity notice keeps current result");UiCapture("17-capacity");
                Click("취소");Check(c.State==ExperimentState.Ready&&pointer.enabled&&!tracker.IsTimingPaused,"capacity cancel resumes work");
                Finish("PASS ui-capacity checks="+checks.Count);yield break;
            }
            tracker.NotifyTreatmentApplied();Check(!c.RequestLibrary()&&tracker.IsTimingPaused&&!pointer.enabled&&!stage.enabled,"general notice pauses input/time");
            Click("확인");Check(pointer.enabled&&!tracker.IsTimingPaused&&stage.enabled,"general notice close restores input/time");
            Check(c.RequestCoin(0),"M1 entry request");
            // 앞선 일반 활동 표본 때문에 정상 폐기 확인을 거칩니다.
            if(!c.IsActive)Click("확인");Check(c.IsActive,"M1 entry");yield return null;UiCapture("01-cleaning");
            Check(!ButtonText("보관 확인")&&pointer.enabled,"cleaning store disabled, work enabled");
            Click("스캐너 꺼짐");Check(c.ScannerEnabled&&ButtonText("스캐너 켜짐"),"scanner label follows state");UiCapture("02-scanner");
            Complete(0);var round=c.RoundId;var sum=surface.SurfaceMask.CurrentDirtSum;var selected=FindFirstObjectByType<CleaningToolSelection>().CurrentTool;
            Check(c.State==ExperimentState.Ready&&ButtonText("보관 확인")&&pointer.enabled&&!tracker.IsTimingPaused&&c.CurrentResult==null,"goal100 allows continued work before store");UiCapture("03-ready");
            Check(c.RequestCoin(1),"explicit replacement asks discard");UiCapture("04-replace");Click("취소");
            Check(c.RoundId==round&&c.State==ExperimentState.Ready&&surface.SurfaceMask.CurrentDirtSum==sum&&pointer.enabled,"replace cancel restores same round");
            Check(c.RequestLeave(),"leave asks discard");UiCapture("05-leave");Click("취소");
            Check(c.RoundId==round&&c.State==ExperimentState.Ready&&pointer.enabled,"leave cancel restores same round");
            Check(c.RequestQuit(),"unkept quit asks warning");UiCapture("06-quit");Click("취소");
            Check(c.RoundId==round&&c.State==ExperimentState.Ready,"quit cancel returns to work");
            tracker.NotifyTreatmentApplied();Check(!c.RequestLibrary()&&tracker.IsTimingPaused&&!pointer.enabled,"notice pauses timer and input");
            var seconds=tracker.ElapsedSeconds;yield return null;yield return null;
            Check(tracker.ElapsedSeconds==seconds,"notice elapsed unchanged across frames");
            c.enabled=false;c.enabled=true;Check(!pointer.enabled&&tracker.IsTimingPaused,"owner enable cannot bypass notice");UiCapture("07-notice");Click("확인");
            Check(pointer.enabled&&!tracker.IsTimingPaused&&c.RoundId==round,"notice close resumes work");
            // 실제 F가 호출하는 자세 복원 API의 데이터 보존. 물리 키 검증으로 주장하지 않습니다.
            pointer.ResetViewToInitialState();Check(c.RoundId==round&&surface.SurfaceMask.CurrentDirtSum==sum&&FindFirstObjectByType<CleaningToolSelection>().CurrentTool==selected,"view reset preserves round/progress/tool");
            Check(c.RequestStore(round),"store confirmation opens");UiCapture("08-confirm");
            uiLock=new FileStream(Path.Combine(directory,CompletedCoinStore.FileName+".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            Check(c.ConfirmDialog(round),"store starts against real exclusive file lock");while(c.PersistenceBusy)yield return null;
            Check(c.State==ExperimentState.SaveFailed&&c.LastError.Contains("Sharing violation"),"actual OS file lock error reaches dialog");UiCapture("09-error");
            Check(c.RequestQuit(),"failed store -> quit warning");UiCapture("10-error-quit");Click("취소");
            Check(c.State==ExperimentState.SaveFailed&&ButtonText("같은 결과 재시도")&&ButtonText("청소로 돌아가기"),"quit cancel restores retry/return buttons");
            ReleaseUiFiles();Click("청소로 돌아가기");Check(c.State==ExperimentState.Ready&&pointer.enabled,"precommit failure returns to same work");
            // 유효 오류 길이를 상한 없이 가정하지 않습니다. 합성 긴 문구로 스크롤 경계만 추가 검사합니다.
            var view=(CleaningExperimentView)typeof(CleaningExperimentController).GetField("view",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(c);
            var stress=string.Concat(Enumerable.Repeat("긴 저장 경로 오류와 복구 안내를 확인합니다. 0123456789\n",48))+"마지막 안내: 원본 보존 후 청소로 돌아가기";
            view.ShowSystemDialog(stress,()=>view.HideDialog(),()=>view.HideDialog());yield return null;UiCapture("11-overflow-top");
            var scroll=FindObjectsByType<ScrollRect>(FindObjectsSortMode.None).Single(x=>x.name=="RB.DialogViewport");
            Check(scroll.content.rect.height>scroll.viewport.rect.height&&scroll.viewport.rect.height==400,"long body bounded with scroll overflow");
            scroll.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-10000)});yield return null;UiCapture("12-overflow-bottom");
            Check(scroll.verticalNormalizedPosition<.001f&&ButtonText("확인")&&ButtonText("취소"),"wheel event reaches final line; buttons remain independent");
            if(!editorRun)
            {
                // 카메라 임시 전환 캡처의 동적 글꼴/clip 차이와 실제 Overlay 표시를 구분합니다.
                yield return null;yield return null;var native=Path.Combine(directory,"12-overflow-native-overlay.png");ScreenCapture.CaptureScreenshot(native);
                var until=Time.realtimeSinceStartup+5;while(!File.Exists(native)&&Time.realtimeSinceStartup<until)yield return null;
                Check(File.Exists(native),"native Overlay screenshot produced");
            }
            Click("취소");
            uiFiles=new UiDelayedWrite();c.UseValidationFiles(uiFiles);Check(c.RequestStore(round)&&c.ConfirmDialog(round),"second UI store starts");
            while(!uiFiles.Entered)yield return null;UiCapture("13-saving");
            Check(!ButtonText("확인")&&!ButtonText("취소")&&tracker.IsTimingPaused&&!pointer.enabled,"saving has no active dialog actions and blocks work");
            Check(c.RequestQuit()&&c.State==ExperimentState.Saving,"quit during write waits");UiCapture("14-saving-quit");
            uiFiles.Release=true;while(c.PersistenceBusy)yield return null;
            Check(c.State==ExperimentState.QuitConfirmation,"write completion opens quit confirmation");Click("취소");
            Check(c.State==ExperimentState.Stored&&c.CurrentResult!=null&&!ButtonText("파일 보관됨"),"cancel after write returns to stored readonly");UiCapture("15-stored");
            Check(c.RequestLibrary(),"stored library opens");UiCapture("16-library");Click("목록 닫기");
            Check(c.ViewStoredResult(c.CurrentResult.ResultId)&&c.State==ExperimentState.Viewing&&!ButtonText("관찰 전용"),"stored result opens readonly view");UiCapture("17-viewing");
            Check(c.RequestCoin(1)&&c.State==ExperimentState.Cleaning,"new explicit M2 work from view");yield return null;UiCapture("18-new-M2");
            Check(c.RequestLeave(),"new M2 leave warns");Click("확인");Check(!c.IsActive,"explicit discard leaves experiment");
            Finish("PASS ui-flow checks="+checks.Count+"; UI callbacks, OS file lock, actual frames, synthetic overflow wheel event; not physical input");
        }
        private void UiCapture(string name)
        {
            var canvas=FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(x=>x.isRootCanvas);var camera=pointer.GameplayCamera;
            var mode=canvas.renderMode;var world=canvas.worldCamera;var plane=canvas.planeDistance;var old=camera.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.1f;Canvas.ForceUpdateCanvases();
                var texts=canvas.GetComponentsInChildren<Text>();
                foreach(var text in texts)Check(text.preferredHeight<=text.rectTransform.rect.height+1,"text height fits "+name+"/"+text.name);
                var body=texts.FirstOrDefault(x=>x.name=="RB.DialogText");
                if(body!=null)
                {
                    var viewport=body.transform.parent.GetComponent<RectTransform>();
                    var card=viewport.parent.GetComponent<RectTransform>();
                    Check(Mathf.Abs(body.rectTransform.rect.width-600)<.1f&&card.rect.height<=560.1f,"dialog first-show width/bounded height "+name);
                    var corners=new Vector3[4];viewport.GetWorldCorners(corners);var bottom=card.InverseTransformPoint(corners[0]).y;
                    foreach(var button in card.GetComponentsInChildren<Button>()){button.GetComponent<RectTransform>().GetWorldCorners(corners);Check(card.InverseTransformPoint(corners[1]).y<bottom,"dialog buttons outside body "+name);}
                }
                File.WriteAllLines(Path.Combine(directory,name+".txt"),texts.Select(x=>x.name+" rect="+x.rectTransform.rect+" preferredHeight="+x.preferredHeight+" text="+x.text.Replace("\n"," / ")));
                camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());
            }
            finally{camera.targetTexture=old;RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=world;canvas.planeDistance=plane;Canvas.ForceUpdateCanvases();RenderTexture.ReleaseTemporary(rt);Destroy(image);}
        }
        private void ReleaseUiFiles(){uiLock?.Dispose();uiLock=null;if(uiFiles!=null)uiFiles.Release=true;}
        private sealed class UiDelayedWrite:SaveFileOperations
        {
            public volatile bool Entered,Release;
            public override void WriteNew(string path,byte[] bytes)
            {
                Entered=true;var end=DateTime.UtcNow.AddSeconds(15);while(!Release&&DateTime.UtcNow<end)Thread.Sleep(5);
                if(!Release)throw new IOException("UI validation gate timeout");base.WriteNew(path,bytes);
            }
        }
    }
}
#endif
