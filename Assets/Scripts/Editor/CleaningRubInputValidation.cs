using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Progression;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>별도 가상 장치와 실제 Play 프레임으로 입력→포인터→A/B 경로를 검사합니다. OS 마우스는 움직이지 않습니다.</summary>
    public static class CleaningRubInputValidation
    {
        private static Mouse mouse;
        private static Keyboard keyboard;
        private static Mouse originalMouse;
        private static Keyboard originalKeyboard;
        private static GameObject probe;
        private static CoinSurfacePointer pointer;
        private static ObjectDirt[] dirt;
        private static CoinSurfaceContamination surface;
        private static CleaningProgressTracker progress;
        private static Camera camera;
        private static Transform body;
        private static Vector2 origin, end, lastPosition, uiPosition;
        private static float[] previousA;
        private static double previousB, previousP;
        private static int step, queuedFrame, checkedFrame, movingChanges, checks;
        public static string Result { get; private set; } = "Not run";

        public static string Begin()
        {
            if(!Application.isPlaying||!EditorApplication.isPaused||probe!=null)throw new InvalidOperationException("Fresh paused Play required.");
            var owner=UnityEngine.Object.FindFirstObjectByType<CleaningStageController>();
            if(owner==null||owner.State!=CleaningStageState.Cleaning||owner.StageId!="stage-01")throw new InvalidOperationException("Initial stage-01 required.");
            pointer=owner.GetComponent<CoinSurfacePointer>();progress=owner.GetComponent<CleaningProgressTracker>();
            surface=UnityEngine.Object.FindFirstObjectByType<CoinSurfaceContamination>();camera=Camera.main;body=surface.transform;
            dirt=owner.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ObjectDirt>(true)).Where(d=>d.gameObject.activeInHierarchy&&d.transform.parent.name=="StageDirt").ToArray();
            var bounds=pointer.SurfaceLocalBounds;
            var point=body.TransformPoint(bounds.center+new Vector3(-.15f,bounds.extents.y,0));
            origin=camera.WorldToScreenPoint(point);end=origin;lastPosition=origin;
            var button=owner.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Button>(true)).Single(b=>b.name=="DryClothButton");
            uiPosition=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            if(!Application.isFocused){Result="BLOCKED: GameView focus unavailable. No focus override.";return Result;}
            originalMouse=Mouse.current;originalKeyboard=Keyboard.current;
            mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
            step=checks=movingChanges=0;queuedFrame=checkedFrame=-1;
            Capture();
            probe=new GameObject("S3.InputFrameProbe"){hideFlags=HideFlags.DontSave};probe.AddComponent<CleaningRubInputFrameProbe>();
            InputSystem.onBeforeUpdate+=Queue;
            EditorApplication.playModeStateChanged+=OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload+=OnReload;
            Result="Running actual frames with synthetic Input System devices";
            EditorApplication.isPaused=false;
            return Result;
        }

        private static void Capture()
        {
            previousA=dirt.Select(d=>d.CurrentDurability).ToArray();
            previousB=surface.SurfaceMask.CurrentDirtSum;previousP=surface.SurfaceMask.CurrentPolishSum;
        }

        private static void Queue()
        {
            if(InputState.currentUpdateType!=InputUpdateType.Dynamic||queuedFrame==Time.frameCount||probe==null)return;
            if(!Application.isFocused){Finish("BLOCKED: focus lost during synthetic frame test; no override");return;}
            queuedFrame=Time.frameCount;
            var position=end;
            if(step<=12)position=origin;
            else if(step<=36){position=origin+new Vector2((step-12)*1.5f,0);end=position;}
            else if(step==62||step==63)position=uiPosition+new Vector2(step==63?3:0,0);
            else if(step==65){end+=new Vector2(6,0);position=end;}
            var state=new MouseState{position=position,delta=position-lastPosition,scroll=step==68?new Vector2(0,1):Vector2.zero};
            state=state.WithButton(MouseButton.Left,step!=49&&step!=72).WithButton(MouseButton.Middle,step==66);
            InputSystem.QueueStateEvent(mouse,state);
            InputSystem.QueueStateEvent(keyboard,step==70?new KeyboardState(Key.F):new KeyboardState());
            lastPosition=position;
        }

        public static void Observe()
        {
            if(probe==null||queuedFrame!=Time.frameCount||checkedFrame==queuedFrame)return;
            checkedFrame=queuedFrame;
            try
            {
                var unchanged=previousB==surface.SurfaceMask.CurrentDirtSum&&previousP==surface.SurfaceMask.CurrentPolishSum&&dirt.Select(d=>d.CurrentDurability).SequenceEqual(previousA);
                var moving=(step>=13&&step<=36)||step==65;
                if(moving){if(!unchanged)movingChanges++;}
                else if(!unchanged)throw new InvalidOperationException("Stationary/boundary frame changed cleaning at step="+step);
                if(step<=12&&progress.HasStarted)throw new InvalidOperationException("Stationary hold started timer.");
                if(step==62||step==63){if(!pointer.IsPointerOverUi)throw new InvalidOperationException("Synthetic UI hit missing.");}
                checks++;Capture();step++;
                if(step==87)
                {
                    if(movingChanges==0||!progress.HasStarted)throw new InvalidOperationException("Drag produced no actual cleaning/timer.");
                    Finish("s3-input actual Play frames PASS checks="+checks+" movingChanges="+movingChanges+". InputSystem virtual Mouse/Keyboard→actual pointer/A/B: first/hold/reclick/UI/MMB/Wheel/F, no OS input or focus override.");
                }
            }
            catch(Exception error){Finish("FAIL: "+error.Message);}
        }

        private static void Finish(string result)
        {
            Result=result;InputSystem.onBeforeUpdate-=Queue;
            EditorApplication.playModeStateChanged-=OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload-=OnReload;
            if(mouse!=null){InputSystem.RemoveDevice(mouse);mouse=null;}
            if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}
            originalMouse?.MakeCurrent();originalKeyboard?.MakeCurrent();
            if(probe!=null)UnityEngine.Object.Destroy(probe);probe=null;
            if(Application.isPlaying)EditorApplication.isPaused=true;
        }

        private static void OnPlayState(PlayModeStateChange state)
        { if(state==PlayModeStateChange.ExitingPlayMode)Finish("Interrupted: Play stopped; synthetic devices removed"); }
        private static void OnReload()=>Finish("Interrupted: assembly reload; synthetic devices removed");

        public static string Cancel()
        {
            if(probe!=null)Finish("Cancelled synthetic frame test");
            return Result;
        }
    }

    public sealed class CleaningRubInputFrameProbe : MonoBehaviour
    {
        private void LateUpdate()=>CleaningRubInputValidation.Observe();
    }
}
