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
    public static class CleaningExperimentFollowupValidation
    {
        private static void Check(bool value,string name,ref int count)
        {if(!value)throw new InvalidOperationException("Rb followup FAIL: "+name);count++;}

        public static string RunScanner()
        {
            if(!Application.isPlaying||!EditorApplication.isPaused)throw new InvalidOperationException("Fresh Play/Pause required");
            var c=UnityEngine.Object.FindFirstObjectByType<CleaningExperimentController>();
            if(c.IsActive||!c.RequestCoin(0))throw new InvalidOperationException("Fresh normal round required");
            var surface=UnityEngine.Object.FindFirstObjectByType<CoinSurfaceContamination>();
            var pointer=UnityEngine.Object.FindFirstObjectByType<CoinSurfacePointer>();var camera=pointer.GameplayCamera;
            var root=(Transform)new SerializedObject(c).FindProperty("coinRoot").objectReferenceValue;
            var original=root.rotation;var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);
            var read=new Texture2D(1280,720,TextureFormat.RGBA32,false);var checks=0;
            var initialSum=surface.SurfaceMask.CurrentDirtSum;var initialRevision=surface.SurfaceMask.Revision;
            var lines=c.Dirt.Select(d=>d.GetComponentInChildren<LineRenderer>(true)).ToArray();
            var path=".utmp/step3-rb-followup";Directory.CreateDirectory(path);
            Color32[] Render(){camera.targetTexture=target;camera.Render();RenderTexture.active=target;read.ReadPixels(new Rect(0,0,1280,720),0,0);read.Apply();return read.GetPixels32();}
            try
            {
                Check(c.ToggleScanner(),"scanner on",ref checks);
                foreach(var angle in new[]{0,70,90,110,180})
                {
                    root.rotation=Quaternion.AngleAxis(angle,camera.transform.right)*original;
                    var pixels=Render();File.WriteAllBytes(path+"/after-"+angle+".png",read.EncodeToPNG());
                    var front=angle<90;var grazing=angle==90;
                    for(var i=0;i<lines.Length;i++)Check(lines[i].enabled==(!grazing&&(i<2)==front),"visible face "+angle+"/"+i,ref checks);
                    for(var i=0;i<lines.Length;i++)if(grazing||((i<2)!=front))lines[i].gameObject.SetActive(false);
                    var withoutRear=Render();Check(pixels.SequenceEqual(withoutRear),"hidden markers affect no pixels "+angle,ref checks);
                    foreach(var line in lines)line.gameObject.SetActive(true);
                }
                root.rotation=original;c.Dirt[0].ApplyCleaning(100);Render();
                Check(!lines[0].gameObject.activeInHierarchy&&!lines[0].enabled&&lines[1].enabled&&!lines[2].enabled&&!lines[3].enabled,"removed/remaining marker source",ref checks);
                c.enabled=false;Render();Check(lines.All(l=>!l.enabled),"disabled owner hides markers",ref checks);
                c.enabled=true;Render();Check(lines[1].enabled&&!lines[2].enabled&&!lines[3].enabled,"enabled owner restores visible remaining face",ref checks);
                Check(c.ToggleScanner(),"scanner off",ref checks);Render();
                Check(lines.All(l=>!l.enabled)&&surface.SurfaceMask.CurrentDirtSum==initialSum&&surface.SurfaceMask.Revision==initialRevision&&surface.Polish==0,"scanner preserves B/P source",ref checks);
                Check(!c.GetComponent<CleaningProgressTracker>().HasStarted,"scanner starts no timer",ref checks);
            }
            finally
            {
                root.rotation=original;foreach(var line in lines)if(line!=null)line.gameObject.SetActive(true);
                camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(read);
                if(c.IsActive){c.RequestLeave();c.ConfirmDialog(c.RoundId);}
            }
            return "Rb scanner camera PASS checks="+checks+" angles=0/70/90/110/180; hidden markers pixel-identical; removed/remaining/B/P/time verified; "+path;
        }

        private static Mouse mouse,oldMouse;
        private static Keyboard keyboard,oldKeyboard;
        private static GameObject probe;
        private static CoinSurfacePointer pointer;
        private static CleaningExperimentController experiment;
        private static CoinSurfaceContamination inputSurface;
        private static Vector2 outside,ui,last;
        private static Quaternion previousRotation;
        private static double previousDirt;
        private static float[] previousA;
        private static int step,queuedFrame,checkedFrame,checks;
        private static bool priorBackground;
        private static EditorWindow gameWindow;
        private static InputActionAsset inputAsset;
        private static ReadOnlyArray<InputDevice>? priorDevices;
        private static double deadline;
        public static string RotationResult {get;private set;}="Not run";

        public static string BeginRotation()
        {
            if(!Application.isPlaying||!EditorApplication.isPaused||probe!=null)throw new InvalidOperationException("Fresh Play/Pause required");
            experiment=UnityEngine.Object.FindFirstObjectByType<CleaningExperimentController>();
            if(experiment.IsActive||!experiment.RequestCoin(0))throw new InvalidOperationException("Normal round required");
            pointer=experiment.GetComponent<CoinSurfacePointer>();inputSurface=UnityEngine.Object.FindFirstObjectByType<CoinSurfaceContamination>();
            gameWindow=Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>w.GetType().Name=="GameView");gameWindow?.Focus();
            if(!Application.isFocused){experiment.RequestLeave();experiment.ConfirmDialog(experiment.RoundId);return RotationResult="BLOCKED: actual GameView focus unavailable; no override";}
            outside=new Vector2(pointer.GameplayCamera.pixelWidth*.15f,pointer.GameplayCamera.pixelHeight*.5f);last=outside;
            var button=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="RB.Button.도구");
            Canvas.ForceUpdateCanvases();
            ui=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
            oldMouse=Mouse.current;oldKeyboard=Keyboard.current;mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
            // Value 액션의 실제/가상 포인터 크기 경쟁을 막는 검사 전용 장치 범위입니다.
            inputAsset=(InputActionAsset)new SerializedObject(pointer).FindProperty("inputActions").objectReferenceValue;
            priorDevices=inputAsset.devices;inputAsset.devices=new InputDevice[]{mouse,keyboard};
            priorBackground=Application.runInBackground;Application.runInBackground=true;
            step=checks=0;queuedFrame=checkedFrame=-1;CaptureInput();
            probe=new GameObject("RB.FollowupInputProbe"){hideFlags=HideFlags.DontSave};probe.AddComponent<CleaningExperimentRotationProbe>();
            InputSystem.onBeforeUpdate+=Queue;EditorApplication.playModeStateChanged+=OnPlay;AssemblyReloadEvents.beforeAssemblyReload+=OnReload;
            deadline=EditorApplication.timeSinceStartup+8;EditorApplication.update+=CheckDeadline;
            EditorApplication.isPaused=false;return RotationResult="Running synthetic devices → actual Play frames";
        }
        private static void CaptureInput()
        {previousRotation=inputSurface.transform.rotation;previousDirt=inputSurface.SurfaceMask.CurrentDirtSum;previousA=experiment.Dirt.Select(d=>d.CurrentDurability).ToArray();}
        private static void Queue()
        {
            if(InputState.currentUpdateType!=InputUpdateType.Dynamic||probe==null||queuedFrame==Time.frameCount)return;
            if(!Application.isFocused)
            {
                if(step==20)
                {
                    try{Check(!pointer.IsRotating,"actual GameView focus loss cancels rotation",ref checks);Finish("Rb MMB actual Play frames PASS checks="+checks+"; background/UI-start/held-return/modal/offscreen/LMB-no-clean/F/focus-loss; virtual devices, not OS mouse");}
                    catch(Exception e){Finish("FAIL: "+e.Message);}
                    gameWindow?.Focus();
                }
                else Finish("BLOCKED: focus lost; no override");
                return;
            }
            queuedFrame=Time.frameCount;
            var position=(step==4||step==5)?ui:outside;
            if(step==2||step==9||step==14)position+=new Vector2(35,0);
            if(step==16)position=new Vector2(-50,50);
            var middle=new[]{1,2,5,6,8,9,10,11,13,14,16,19,20}.Contains(step);
            var state=new MouseState{position=position,delta=position-last}.WithButton(MouseButton.Middle,middle).WithButton(MouseButton.Left,middle);
            InputSystem.QueueStateEvent(mouse,state);InputSystem.QueueStateEvent(keyboard,step==3?new KeyboardState(Key.F):new KeyboardState());last=position;
        }
        public static void ObserveRotation()
        {
            if(probe==null||queuedFrame!=Time.frameCount||checkedFrame==queuedFrame)return;checkedFrame=queuedFrame;
            try
            {
                Check(inputSurface.SurfaceMask.CurrentDirtSum==previousDirt&&inputSurface.Polish==0&&experiment.Dirt.Select(d=>d.CurrentDurability).SequenceEqual(previousA),"MMB no cleaning "+step,ref checks);
                if(step==1||step==2||step==8||step==13||step==14)Check(pointer.IsRotating,"background start/hold "+step,ref checks);
                if(step==2||step==14)Check(Quaternion.Angle(previousRotation,inputSurface.transform.rotation)>.01f,"actual rotation delta",ref checks);
                if(step==1)Check(!pointer.HasSurfaceHit,"background no coin hit",ref checks);
                if(step==5)Check(pointer.IsPointerOverUi&&!pointer.IsRotating,"UI start blocked (target="+ui+", actual="+Mouse.current.position.ReadValue()+", overUI="+pointer.IsPointerOverUi+", rotating="+pointer.IsRotating+")",ref checks);
                if(step==6||step==11||step==16)Check(!pointer.IsRotating,"held UI/modal return/offscreen blocked "+step+" actionPosition="+inputAsset.FindAction("Cleaning/PointerPosition").ReadValue<Vector2>()+" virtualPosition="+mouse.position.ReadValue(),ref checks);
                if(step==9)Check(experiment.RequestLeave()&&!pointer.enabled&&!pointer.IsRotating,"confirmation cancels rotation",ref checks);
                if(step==10)Check(experiment.CancelDialog(experiment.RoundId),"cancel confirmation",ref checks);
                if(step==17)Check(!experiment.GetComponent<CleaningProgressTracker>().HasStarted,"rotation no timer",ref checks);
                if(step==19)
                {
                    Check(pointer.IsRotating,"rotation before focus loss",ref checks);
                    if(SceneView.lastActiveSceneView==null){Finish("Rb MMB PASS checks="+checks+"; focus-loss NOT RUN (no SceneView)");return;}
                    SceneView.lastActiveSceneView.Focus();
                }
                if(step>24){Finish("BLOCKED: GameView focus loss not delivered");return;}
                CaptureInput();step++;
            }
            catch(Exception error){Finish("FAIL: "+error.Message);}
        }
        private static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("Interrupted: Play stopped");}
        private static void OnReload()=>Finish("Interrupted: assembly reload");
        private static void CheckDeadline(){if(probe!=null&&EditorApplication.timeSinceStartup>deadline){Finish("BLOCKED: actual frame/focus delivery timeout8s");gameWindow?.Focus();}}
        private static void Finish(string result)
        {
            RotationResult=result;InputSystem.onBeforeUpdate-=Queue;EditorApplication.playModeStateChanged-=OnPlay;AssemblyReloadEvents.beforeAssemblyReload-=OnReload;
            EditorApplication.update-=CheckDeadline;
            if(inputAsset!=null)inputAsset.devices=priorDevices;inputAsset=null;
            if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);mouse=null;keyboard=null;oldMouse?.MakeCurrent();oldKeyboard?.MakeCurrent();
            if(probe!=null)UnityEngine.Object.Destroy(probe);probe=null;if(Application.isPlaying)EditorApplication.isPaused=true;
            Application.runInBackground=priorBackground;
        }

        public static string MeasureExistingSpeed()
        {
            var definition=AssetDatabase.LoadAssetAtPath<CleaningExperimentDefinition>(CleaningExperimentTools.DefinitionPath);
            var tool=definition.Tools.Single(t=>t.ToolId==CleaningToolId.ExperimentCloth);var reaction=definition.Coins[0].ReactionProfile;
            var matrix=Matrix4x4.Scale(new Vector3(2,.1f,2));var bounds=new Bounds(Vector3.zero,new Vector3(1,2,1));
            CoinSurfaceContact Contact(float x){CoinSurfaceMapper.TryMapPoint(matrix,bounds,new Vector3(x,.1f,0),Vector3.up,out var contact);return contact;}
            var text="Existing kernel speed probe: 60Hz; M1 untreated cloth; radius="+tool.BrushRadius+" rate="+tool.SurfaceDirtRemovalPerSecond+" coefficient="+reaction.SurfaceCoefficient(tool.ToolId,false)+"; initialB1/res128; no input devices";
            foreach(var repeated in new[]{false,true})foreach(var speed in new[]{.3f,.6f,1.2f,2.4f})
            {
                var data=new CoinSurfaceMaskData(128,1,CoinSurfaceDirtPattern.Uniform);var motion=new CleaningRubMotion();var stroke=new CoinSurfaceBrushStroke();
                var steps=repeated?240:Mathf.RoundToInt(60/speed);double budget=0;var start=Contact(-.5f);
                motion.Sample(0,true,start,matrix,tool,data,1f/60);
                for(var frame=1;frame<=steps;frame++)
                {
                    var distance=repeated?Mathf.PingPong(speed*frame/60,1):frame/(float)steps;
                    var sample=motion.Sample(frame,true,Contact(-.5f+distance),matrix,tool,data,1f/60);
                    budget+=sample.EffectiveSeconds;
                    if(sample.IsRubbing)stroke.ApplyApprovedSweep(data,sample.Start,sample.End,matrix,bounds,tool,tool.BrushRadius,tool.SurfaceDirtRemovalPerSecond,sample.EffectiveSeconds,0,reaction,tool.ToolId);
                }
                var index=64*128+64;
                text+=$"\n{(repeated?"4s triangle":"1m single pass")} speed={speed:F1} frames={steps} effective={budget:F6}s targetRemoval={1-data.GetDirt(CoinSurfaceFace.Front,index):F6} frontRemoval={1-data.GetCurrentDirtSum(CoinSurfaceFace.Front)/data.GetInitialDirtSum(CoinSurfaceFace.Front):F6}";
            }
            return text;
        }
    }
    public sealed class CleaningExperimentRotationProbe:MonoBehaviour
    {private void LateUpdate()=>CleaningExperimentFollowupValidation.ObserveRotation();}
}
