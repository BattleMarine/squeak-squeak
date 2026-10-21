using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Experiment;

namespace SqueakSqueak.EditorValidation
{
    // Editor 전용 S3-06 재현. 제품 저장 경로/씬/설정 에셋을 수정하지 않습니다.
    public static class CleaningUiFlowValidation
    {
        private static FileStream heldLock;
        public static string Capture(string directory,string name)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play required");
            directory=CleaningExperimentController.ValidateIsolatedPath(directory);Directory.CreateDirectory(directory);
            var camera=Camera.main;var canvas=UnityEngine.Object.FindFirstObjectByType<CleaningExperimentController>().GetComponentInChildren<Canvas>();
            if(canvas==null)canvas=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(x=>x.isRootCanvas);
            var mode=canvas.renderMode;var world=canvas.worldCamera;var distance=canvas.planeDistance;
            var target=camera.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.1f;Canvas.ForceUpdateCanvases();
                camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());
                var lines=canvas.GetComponentsInChildren<Text>().Select(t=>t.name+" text="+t.text.Replace("\n"," / ")+"; rect="+t.rectTransform.rect.size+"; preferred="+t.preferredWidth+"x"+t.preferredHeight+"; vertices="+t.cachedTextGenerator.vertexCount).ToArray();
                File.WriteAllLines(Path.Combine(directory,name+".txt"),lines);
                var body=canvas.GetComponentsInChildren<Text>().FirstOrDefault(t=>t.name=="RB.DialogText");
                return name+" canvas="+((RectTransform)canvas.transform).rect.size+(body==null?"":" body="+body.rectTransform.rect.size+" preferredHeight="+body.preferredHeight+" textLength="+body.text.Length);
            }
            finally{camera.targetTexture=target;RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=world;canvas.planeDistance=distance;Canvas.ForceUpdateCanvases();RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
        public static void Complete()
        {
            var c=UnityEngine.Object.FindFirstObjectByType<CleaningExperimentController>();var s=UnityEngine.Object.FindFirstObjectByType<CoinSurfaceContamination>();
            foreach(var d in c.Dirt)d.ApplyCleaning(100);
            foreach(var f in new[]{CoinSurfaceFace.Front,CoinSurfaceFace.Back})for(var i=0;i<s.SurfaceMask.TexelCount;i++)s.SurfaceMask.TrySetDirt(f,i,s.SurfaceMask.GetDirt(f,i)*(i%101==0?.01f:0));
            s.UploadSurfaceMasks();c.EvaluateGoal();
        }
        public static string HoldStoreLock()
        {
            var c=UnityEngine.Object.FindFirstObjectByType<CleaningExperimentController>();
            var dir=CleaningExperimentController.ValidateIsolatedPath(c.SaveDirectory);
            var path=Path.Combine(dir,"completed-coins-v1.sqsave.lock");
            heldLock=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            AssemblyReloadEvents.beforeAssemblyReload+=ReleaseLock;
            EditorApplication.playModeStateChanged+=ReleaseOnStop;return path;
        }
        public static void ReleaseLock(){heldLock?.Dispose();heldLock=null;AssemblyReloadEvents.beforeAssemblyReload-=ReleaseLock;EditorApplication.playModeStateChanged-=ReleaseOnStop;}
        private static void ReleaseOnStop(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)ReleaseLock();}

        private static Mouse mouse,oldMouse;
        private static Keyboard keyboard,oldKeyboard;
        private static InputActionAsset asset;
        private static ReadOnlyArray<InputDevice>? priorDevices;
        private static GameObject probe;
        private static CleaningExperimentController owner;
        private static CoinSurfacePointer pointer;
        private static Transform coin;
        private static Quaternion before;
        private static Vector2 last,position;
        private static int step,queued=-1,observed=-1;
        private static double timeout;
        private static bool background;
        private static CleaningProgressTracker timer;
        private static float noticeSeconds;
        private static Vector2 outside,ui;
        private static readonly System.Collections.Generic.List<string> inputChecks=new();
        private static bool rubMode;
        private static CoinSurfaceContamination rubSurface;
        private static double rubBefore,blockedDirt;
        private static float[] blockedA;
        private static Quaternion initialRotation,modifiedRotation;
        private static float initialZoom;
        private static string reportPath;
        public static string InputReport {get;private set;}="Not run";
        public static string BeginHeldNotice(string directory,bool rubAndView=false)
        {
            if(!Application.isPlaying||!EditorApplication.isPaused||probe!=null)throw new InvalidOperationException("Idle Play/Pause required");
            rubMode=rubAndView;reportPath=Path.Combine(CleaningExperimentController.ValidateIsolatedPath(directory),rubMode?"rub-ui-F.txt":"held-notice.txt");
            owner=UnityEngine.Object.FindFirstObjectByType<CleaningExperimentController>();
            if(owner.State!=ExperimentState.Cleaning&&owner.State!=ExperimentState.Ready)throw new InvalidOperationException("Working round required");
            Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>w.GetType().Name=="GameView")?.Focus();
            if(!Application.isFocused)return InputReport="BLOCKED actual GameView focus unavailable (no override)";
            pointer=owner.GetComponent<CoinSurfacePointer>();coin=(Transform)new SerializedObject(pointer).FindProperty("coinTransform").objectReferenceValue;
            rubSurface=UnityEngine.Object.FindFirstObjectByType<CoinSurfaceContamination>();
            pointer.ResetViewToInitialState();initialRotation=coin.rotation;initialZoom=pointer.GameplayCamera.orthographicSize;
            if(rubMode){owner.SelectTool(1);rubBefore=rubSurface.SurfaceMask.CurrentDirtSum;}
            timer=owner.GetComponent<CleaningProgressTracker>();timer.NotifyTreatmentApplied();
            asset=(InputActionAsset)new SerializedObject(pointer).FindProperty("inputActions").objectReferenceValue;
            priorDevices=asset.devices;oldMouse=Mouse.current;oldKeyboard=Keyboard.current;mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();asset.devices=new InputDevice[]{mouse,keyboard};
            background=Application.runInBackground;Application.runInBackground=true;
            outside=new Vector2(pointer.GameplayCamera.pixelWidth*.15f,pointer.GameplayCamera.pixelHeight*.5f);position=outside;last=position;
            Canvas.ForceUpdateCanvases();ui=RectTransformUtility.WorldToScreenPoint(null,UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(x=>x.name=="RB.Button.도구").transform.position);
            inputChecks.Clear();
            step=0;queued=observed=-1;timeout=EditorApplication.timeSinceStartup+10;
            probe=new GameObject("S306.HeldNoticeProbe"){hideFlags=HideFlags.DontSave};probe.AddComponent<CleaningUiFlowInputProbe>();
            InputSystem.onBeforeUpdate+=Queue;EditorApplication.update+=Deadline;EditorApplication.playModeStateChanged+=OnStop;AssemblyReloadEvents.beforeAssemblyReload+=OnReload;
            EditorApplication.isPaused=false;return InputReport="Running actual frames / virtual Mouse";
        }
        private static void Queue()
        {
            if(probe==null||InputState.currentUpdateType!=InputUpdateType.Dynamic||queued==Time.frameCount)return;
            if(!Application.isFocused){FinishInput("BLOCKED focus lost (not overridden)");return;}
            queued=Time.frameCount;
            if(rubMode)
            {
                position=step==11?ui:step<=9?(Vector2)pointer.GameplayCamera.WorldToScreenPoint(coin.position)+new Vector2(-30+Mathf.Min(step,8)*10,0):outside;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=position,delta=position-last}.WithButton(MouseButton.Left,step>=1&&step<=9));
                InputSystem.QueueStateEvent(keyboard,step==11||step==13?new KeyboardState(Key.F):new KeyboardState());
            }
            else
            {
                position=step==12?ui:outside+new Vector2((step%3)*20,0);
                var middle=step>=1&&step!=10&&step!=14&&step!=16;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=position,delta=position-last}.WithButton(MouseButton.Middle,middle));
            }
            last=position;
        }
        public static void Observe()
        {
            if(probe==null||queued!=Time.frameCount||observed==queued)return;observed=queued;
            try
            {
                void Require(bool value,string name){if(!value)throw new InvalidOperationException(name);inputChecks.Add(name);}
                if(rubMode)
                {
                    if(step==3)
                    {
                        Require(rubSurface.SurfaceMask.CurrentDirtSum<rubBefore,"positive actual-frame LMB cloth removes B");
                        owner.RequestLibrary();blockedDirt=rubSurface.SurfaceMask.CurrentDirtSum;blockedA=owner.Dirt.Select(x=>x.CurrentDurability).ToArray();noticeSeconds=timer.ElapsedSeconds;
                    }
                    if(step==8)
                    {
                        Require(rubSurface.SurfaceMask.CurrentDirtSum==blockedDirt&&owner.Dirt.Select(x=>x.CurrentDurability).SequenceEqual(blockedA),"held LMB behind notice changes no A/B");
                        Require(timer.IsTimingPaused&&timer.ElapsedSeconds==noticeSeconds,"held LMB notice time delta zero");
                        UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x=>x.interactable&&x.GetComponentInChildren<Text>()?.text=="확인").onClick.Invoke();
                    }
                    if(step==10){coin.Rotate(Vector3.right,20,Space.World);pointer.GameplayCamera.orthographicSize=initialZoom+.2f;modifiedRotation=coin.rotation;blockedDirt=rubSurface.SurfaceMask.CurrentDirtSum;}
                    if(step==11)Require(pointer.IsPointerOverUi&&Quaternion.Angle(coin.rotation,modifiedRotation)<.001f&&pointer.GameplayCamera.orthographicSize>initialZoom,"F over ordinary UI does not reset behind UI");
                    if(step==13)
                    {
                        Require(Quaternion.Angle(coin.rotation,initialRotation)<.001f&&Mathf.Abs(pointer.GameplayCamera.orthographicSize-initialZoom)<.001f,"F outside UI restores pose and zoom; angle="+Quaternion.Angle(coin.rotation,initialRotation)+" zoom="+pointer.GameplayCamera.orthographicSize+" initial="+initialZoom+" overUI="+pointer.IsPointerOverUi+" keyPressed="+asset.FindAction("Cleaning/ResetView").WasPressedThisFrame());
                        Require(rubSurface.SurfaceMask.CurrentDirtSum==blockedDirt&&timer.HasStarted&&!timer.IsTimingPaused&&owner.GetComponent<CleaningToolSelection>().CurrentTool.ToolId==CleaningToolId.ExperimentCloth,"F preserves progress/timer/tool");
                        FinishInput("PASS checks="+inputChecks.Count+"; actual focused frames/virtual Mouse+Keyboard; not OS input\n"+string.Join("\n",inputChecks));return;
                    }
                    step++;return;
                }
                if(step==2)
                {
                    Require(pointer.IsRotating,"positive MMB before notice");before=coin.rotation;owner.RequestLibrary();noticeSeconds=timer.ElapsedSeconds;
                }
                if(step==8)
                {
                    Require(!pointer.IsRotating&&Quaternion.Angle(before,coin.rotation)<.001f,"held MMB blocked behind notice");
                    Require(timer.IsTimingPaused&&Mathf.Abs(timer.ElapsedSeconds-noticeSeconds)<.00001f,"notice timer paused, delta zero");
                    UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x=>x.interactable&&x.GetComponentInChildren<Text>()?.text=="확인").onClick.Invoke();
                    Require(pointer.enabled&&!timer.IsTimingPaused,"notice close restores input/time");
                }
                if(step==9)Require(!pointer.IsRotating,"held after notice close cannot resume MMB");
                if(step==11)Require(pointer.IsRotating,"fresh MMB resumes after notice");
                if(step==12){Require(pointer.IsPointerOverUi&&!pointer.IsRotating,"existing MMB entering ordinary UI cancels");before=coin.rotation;}
                if(step==13)Require(!pointer.IsRotating&&Quaternion.Angle(before,coin.rotation)<.001f,"held after leaving ordinary UI cannot resume");
                if(step==15)Require(pointer.IsRotating,"fresh MMB resumes after ordinary UI");
                if(step==16){FinishInput("PASS checks="+inputChecks.Count+"; actual focused Play frames/virtual Mouse; not OS mouse\n"+string.Join("\n",inputChecks));return;}
                step++;
            }
            catch(Exception e){FinishInput("FAIL step="+step+" "+e.Message+"\n"+string.Join("\n",inputChecks));}
        }
        private static void Deadline(){if(probe!=null&&EditorApplication.timeSinceStartup>timeout)FinishInput("BLOCKED 10s frame delivery timeout");}
        private static void OnStop(PlayModeStateChange s){if(s==PlayModeStateChange.ExitingPlayMode)FinishInput("Interrupted stop");}
        private static void OnReload()=>FinishInput("Interrupted reload");
        private static void FinishInput(string value)
        {
            InputReport=value;Directory.CreateDirectory(Path.GetDirectoryName(reportPath));File.WriteAllText(reportPath,value);
            InputSystem.onBeforeUpdate-=Queue;EditorApplication.update-=Deadline;EditorApplication.playModeStateChanged-=OnStop;AssemblyReloadEvents.beforeAssemblyReload-=OnReload;
            if(asset!=null)asset.devices=priorDevices;if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);oldMouse?.MakeCurrent();oldKeyboard?.MakeCurrent();
            if(probe!=null)UnityEngine.Object.Destroy(probe);probe=null;Application.runInBackground=background;
            if(Application.isPlaying)EditorApplication.isPaused=true;
        }
    }
    public sealed class CleaningUiFlowInputProbe:MonoBehaviour {private void LateUpdate()=>CleaningUiFlowValidation.Observe();}
}
