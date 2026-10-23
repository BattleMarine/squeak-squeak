#if UNITY_EDITOR || SQUEAK_SAVE_VALIDATION
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Experiment;

namespace SqueakSqueak.Saving
{
    public sealed partial class SavePlayerProbe
    {
        private Mouse observationMouse,observationOldMouse;
        private Keyboard observationKeyboard,observationOldKeyboard;
        private InputActionAsset observationActions;
        private ReadOnlyArray<InputDevice>? observationDevices;
        private void ReleaseObservationDevices()
        {
            if(observationActions!=null)observationActions.devices=observationDevices;
            if(observationMouse!=null)InputSystem.RemoveDevice(observationMouse);if(observationKeyboard!=null)InputSystem.RemoveDevice(observationKeyboard);
            observationOldMouse?.MakeCurrent();observationOldKeyboard?.MakeCurrent();observationMouse=null;observationKeyboard=null;observationActions=null;
        }
        private IEnumerator RunObservationCursor()
        {
            while(!c.SaveReady){if(c.LastError!=null)throw new Exception(c.LastError);yield return null;}while(c.PersistenceBusy)yield return null;
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var hover=typeof(CoinSurfacePointer).GetMethod("UpdateSurfaceFromScreenPosition",flags);
            var cursor=(SurfaceToolCursor)typeof(CoinSurfacePointer).GetField("toolCursor",flags).GetValue(pointer);
            var dot=(Renderer)typeof(SurfaceToolCursor).GetField("cursorRenderer",flags).GetValue(cursor);
            var ring=(LineRenderer)typeof(SurfaceToolCursor).GetField("rangeRenderer",flags).GetValue(cursor);
            coinTransform=(Transform)IntegrationField("coinRoot");var camera=pointer.GameplayCamera;
            observationActions=(InputActionAsset)typeof(CoinSurfacePointer).GetField("inputActions",flags).GetValue(pointer);observationDevices=observationActions.devices;
            observationOldMouse=Mouse.current;observationOldKeyboard=Keyboard.current;observationMouse=InputSystem.AddDevice<Mouse>();observationKeyboard=InputSystem.AddDevice<Keyboard>();
            observationActions.devices=new InputDevice[]{observationMouse,observationKeyboard};
            Vector2 Center()=>(Vector2)camera.WorldToScreenPoint(coinTransform.position);
            void Queue(bool middle=false,Vector2 delta=default,Vector2 scroll=default,bool reset=false)
            {InputSystem.QueueStateEvent(observationMouse,new MouseState{position=Center(),delta=delta,scroll=scroll}.WithButton(MouseButton.Middle,middle));InputSystem.QueueStateEvent(observationKeyboard,reset?new KeyboardState(Key.F):new KeyboardState());}
            void Hover()=>hover.Invoke(pointer,new object[]{Center()});
            bool Visible()=>cursor.IsVisible&&dot.enabled&&ring.enabled;
            bool Hidden()=>!cursor.IsVisible&&!dot.enabled&&!ring.enabled;
            Queue();yield return null;Hover();Check(Visible(),"normal mode positive hover shows dot and range");
            Check(c.StoredResults.Count>=2,"isolated saved fixtures available");
            var result=c.StoredResults.First();Check(c.ViewStoredResult(result.ResultId),"enter observation from normal mode");Queue();yield return null;Hover();
            if(phase=="observation-repro")
            {
                Check(pointer.enabled&&!surface.ProcessingAllowed&&Visible(),"REPRO confirmed: readonly pointer still displays cleaning cursor after hover");UiCapture("before-observation");
                Finish("PASS reproduction checks="+checks.Count+"; bug observed, not fixed; controlled hover/actual frames");yield break;
            }
            Check(Hidden()&&pointer.enabled,"observation hides both renderers without disabling view input");UiCapture("after-observation");
            var originalFile=File.ReadAllBytes(Path.Combine(directory,CompletedCoinStore.FileName));var storedCount=c.StoredResults.Count;
            foreach(var value in new[]{c.StoredResults.First(),c.StoredResults.Last()})
            {
                Check(c.ViewStoredResult(value.ResultId),"switch legacy/new observation side="+value.HasSide);Queue();yield return null;
                foreach(var degrees in new[]{0,180,90})
                {
                    pointer.ResetViewToInitialState();coinTransform.rotation=Quaternion.AngleAxis(degrees,camera.transform.right)*coinTransform.rotation;Physics.SyncTransforms();
                    Queue();yield return null;Hover();Check(pointer.HasSurfaceHit&&Hidden(),"front/back/side observation cursor hidden "+degrees);
                }
                var data=Digest(c.CurrentResult);var revision=surface.SurfaceMask.Revision;
                Check(!surface.ApplyCleaning(.1f)&&!surface.ApplyDroplet(default,.2f,Time.frameCount)&&!c.SelectTool(2),"observation still rejects work");
                typeof(CoinSurfacePointer).GetMethod("ApplyRotation",flags).Invoke(pointer,new object[]{new Vector2(25,10)});var rotated=coinTransform.rotation;
                var zoom=camera.orthographicSize;typeof(CoinSurfacePointer).GetMethod("ApplyZoom",flags).Invoke(pointer,new object[]{1f});
                Check(camera.orthographicSize<zoom,"observation zoom handler remains functional");pointer.ResetViewToInitialState();
                Check(Quaternion.Angle(rotated,coinTransform.rotation)>.1f&&Mathf.Abs(camera.orthographicSize-zoom)<.001f,"rotation handler and F view-reset API remain functional");
                Hover();Check(Hidden()&&surface.SurfaceMask.Revision==revision&&Digest(c.CurrentResult)==data,"view transforms change no cleaning data or cursor");
            }
            pointer.ResetViewToInitialState();Queue();yield return null;
            if(Application.isFocused)
            {
                var start=coinTransform.rotation;var zoom=camera.orthographicSize;
                Queue(true);yield return null;Queue(true,new Vector2(25,10));yield return null;
                Check(Application.isFocused&&pointer.IsRotating&&Quaternion.Angle(start,coinTransform.rotation)>.1f&&Hidden(),"actual virtual MMB frames rotate observation with no cursor");
                Queue();yield return null;Queue(scroll:new Vector2(0,120));yield return null;
                Check(camera.orthographicSize<zoom&&Hidden(),"actual virtual wheel frames zoom observation with no cursor");
                Queue();yield return null;Queue(reset:true);yield return null;
                Check(Quaternion.Angle(start,coinTransform.rotation)<.01f&&Mathf.Abs(camera.orthographicSize-zoom)<.001f&&Hidden(),"actual virtual F frames restore observation with no cursor");Queue();yield return null;
            }
            else checks.Add("NOT RUN actual MMB/Wheel/F input: focus unavailable; view-handler APIs checked, no focus override");
            Check(File.ReadAllBytes(Path.Combine(directory,CompletedCoinStore.FileName)).SequenceEqual(originalFile),"readonly operations preserve saved file bytes");
            Check(c.RequestCoin(0),"new experiment from observation");Queue();yield return null;Hover();Check(Visible(),"new work restores cursor");
            Complete(0);Check(c.RequestStore(c.RoundId)&&c.ConfirmDialog(c.RoundId),"store new completed fixture");while(c.PersistenceBusy)yield return null;
            Queue();yield return null;Hover();Check(c.State==ExperimentState.Stored&&Hidden()&&pointer.enabled&&c.StoredResults.Count==storedCount+1,"Stored also hides cursor while preserving observation");
            Check(c.RequestCoin(1),"new M2 after Stored");Queue();yield return null;Hover();Check(Visible(),"new M2 work restores cursor");
            Check(c.RequestLeave()&&c.ConfirmDialog(c.RoundId)&&!c.IsActive,"explicit leave returns normal mode");Queue();yield return null;Hover();
            Check(Visible()&&pointer.enabled,"normal mode restores cursor and input");
            // Leave의 일반 단계 재진입은 진행 저장을 시작할 수 있습니다. 작업 완료 후 일반 종료창을 검사합니다.
            while(c.PersistenceBusy)yield return null;
            Check(c.RequestQuit(),"normal quit confirmation opens");Queue();yield return null;Click("돌아가기");Queue();yield return null;Hover();
            Check(Visible()&&pointer.enabled&&!c.IsActive,"normal quit cancel restores cursor/input without experiment policy leak");
            ReleaseObservationDevices();Finish("PASS observation-cursor checks="+checks.Count+"; cursor presentation/state/actual frames; virtual input only when focused, not OS input");
        }
    }
}
#endif
